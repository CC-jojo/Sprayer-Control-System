#include "current_module.h"

#include "app_config.h"
#include "modbus_crc.h"

#include <limits.h>

enum
{
    CURRENT_REGISTER = 0x000A,      /* 电流模块一路模拟量输出寄存器地址。 */
    CURRENT_RETRY_DELAY_MS = 500    /* 通信失败后等待 500 ms 再重试。 */
};

/* 读取 Modbus 帧中的大端 16 位数。 */
static uint16_t ReadU16(const uint8_t *bytes)
{
    return (uint16_t)(((uint16_t)bytes[0] << 8U) | bytes[1]);
}

/* 写入 Modbus 帧中的大端 16 位数。 */
static void WriteU16(uint8_t *bytes, uint16_t value)
{
    bytes[0] = (uint8_t)(value >> 8U);
    bytes[1] = (uint8_t)value;
}

/* 为固定 6 字节载荷追加 CRC，形成 8 字节 RTU 请求。 */
static size_t FinishRequest(uint8_t *request)
{
    const uint16_t crc = Modbus_Crc16(request, 6U);
    request[6] = (uint8_t)crc;
    request[7] = (uint8_t)(crc >> 8U);
    return 8U;
}

/* 初始化后安排写入0微安。 */
void CurrentModule_Init(CurrentModule *module, uint8_t slave_address)
{
    module->slave_address = slave_address;
    module->target = 0U;
    module->readback = 0U;
    module->fault_count = 0U;
    module->retry_remaining_ms = 0U;
    module->state = CURRENT_WRITE_PENDING;
}

/* 直接接收微安目标，超过模块量程上限时钳位。 */
void CurrentModule_SetTargetMicroamps(CurrentModule *module, uint16_t target_microamps)
{
    if (target_microamps > APP_CURRENT_MAX_UA) {
        target_microamps = APP_CURRENT_MAX_UA;
    }
    module->target = target_microamps;
    module->state = CURRENT_WRITE_PENDING;
}

/* 仅在线状态允许发起周期性回读校验。 */
void CurrentModule_RequestVerify(CurrentModule *module)
{
    if (module->state == CURRENT_ONLINE) {
        module->state = CURRENT_VERIFY_PENDING;
    }
}

/* 根据状态构造 0x06 写单寄存器或 0x03 读保持寄存器请求。 */
size_t CurrentModule_BuildRequest(CurrentModule *module, uint8_t *request, size_t capacity)
{
    if (capacity < 8U) {
        return 0U;
    }
    /* 写寄存器 0x000A，并等待模块回显写入内容。 */
    if (module->state == CURRENT_WRITE_PENDING) {
        request[0] = module->slave_address;
        request[1] = 0x06U;
        WriteU16(&request[2], CURRENT_REGISTER);
        WriteU16(&request[4], module->target);
        module->state = CURRENT_WAIT_WRITE;
        return FinishRequest(request);
    }
    /* 读取一个寄存器，用于确认模块实际值与目标值一致。 */
    if (module->state == CURRENT_VERIFY_PENDING) {
        request[0] = module->slave_address;
        request[1] = 0x03U;
        WriteU16(&request[2], CURRENT_REGISTER);
        WriteU16(&request[4], 1U);
        module->state = CURRENT_WAIT_VERIFY;
        return FinishRequest(request);
    }
    return 0U;
}

/* 先检查 CRC 和从站地址，再按照等待状态验证响应格式。 */
bool CurrentModule_OnResponse(CurrentModule *module, const uint8_t *response, size_t length)
{
    if (!Modbus_FrameHasValidCrc(response, length) ||
        (response[0] != module->slave_address)) {
        return false;
    }

    /* 0x06 响应必须完整回显寄存器地址和目标值。 */
    if (module->state == CURRENT_WAIT_WRITE) {
        if ((length != 8U) || (response[1] != 0x06U) ||
            (ReadU16(&response[2]) != CURRENT_REGISTER) ||
            (ReadU16(&response[4]) != module->target)) {
            return false;
        }
        module->state = CURRENT_VERIFY_PENDING;
        return true;
    }

    /* 0x03 响应应含两个数据字节；不一致时重新安排写入。 */
    if (module->state == CURRENT_WAIT_VERIFY) {
        if ((length != 7U) || (response[1] != 0x03U) || (response[2] != 2U)) {
            return false;
        }
        module->readback = ReadU16(&response[3]);
        module->state = (module->readback == module->target)
            ? CURRENT_ONLINE : CURRENT_WRITE_PENDING;
        return true;
    }

    return false;
}

/* 记录一次超时，并转入固定时长的重试等待。 */
void CurrentModule_OnTimeout(CurrentModule *module)
{
    if (module->fault_count < UINT16_MAX) {
        ++module->fault_count;
    }
    module->retry_remaining_ms = CURRENT_RETRY_DELAY_MS;
    module->state = CURRENT_RETRY_WAIT;
}

/* 只在重试等待状态扣减计时，到期后重新安排写命令。 */
void CurrentModule_Tick(CurrentModule *module, uint16_t elapsed_ms)
{
    if (module->state != CURRENT_RETRY_WAIT) {
        return;
    }
    if (elapsed_ms >= module->retry_remaining_ms) {
        module->retry_remaining_ms = 0U;
        module->state = CURRENT_WRITE_PENDING;
    } else {
        module->retry_remaining_ms = (uint16_t)(module->retry_remaining_ms - elapsed_ms);
    }
}
