#include "host_modbus.h"

#include "modbus_crc.h"

#include <string.h>

/* 从 Modbus 帧中读取大端 16 位字段。 */
static uint16_t ReadU16(const uint8_t *bytes)
{
    return (uint16_t)(((uint16_t)bytes[0] << 8U) | bytes[1]);
}

/* 向 Modbus 帧写入大端 16 位字段。 */
static void WriteU16(uint8_t *bytes, uint16_t value)
{
    bytes[0] = (uint8_t)(value >> 8U);
    bytes[1] = (uint8_t)(value & 0xFFU);
}

/* 在有效载荷尾部追加 Modbus CRC（低字节在前），并返回完整帧长。 */
static size_t FinishFrame(uint8_t *frame, size_t payload_length)
{
    const uint16_t crc = Modbus_Crc16(frame, payload_length);
    frame[payload_length] = (uint8_t)(crc & 0xFFU);
    frame[payload_length + 1U] = (uint8_t)(crc >> 8U);
    return payload_length + 2U;
}

/* 构造 Modbus 异常响应；广播请求按照协议不返回任何响应。 */
static size_t ExceptionResponse(
    uint8_t slave,
    uint8_t function,
    MbResult error,
    bool broadcast,
    uint8_t *response,
    size_t capacity)
{
    if (broadcast || capacity < 5U) {
        return 0U;
    }
    response[0] = slave;
    response[1] = (uint8_t)(function | 0x80U);
    response[2] = (uint8_t)error;
    return FinishFrame(response, 3U);
}

/* 支持 0x03 读保持寄存器、0x06 写单寄存器和 0x10 写多寄存器。 */
size_t HostModbus_HandleFrame(
    HostRegisters *registers,
    uint8_t slave_address,
    const uint8_t *request,
    size_t request_length,
    uint8_t *response,
    size_t response_capacity)
{
    /* CRC 错误的帧直接丢弃，避免误执行控制命令。 */
    if (!Modbus_FrameHasValidCrc(request, request_length)) {
        return 0U;
    }

    /* 地址 0 是广播地址：允许写入，但不得回复。 */
    const bool broadcast = request[0] == 0U;
    if (!broadcast && (request[0] != slave_address)) {
        return 0U;
    }

    const uint8_t function = request[1];
    /* 功能码 0x03：连续读取保持寄存器。 */
    if (function == 0x03U) {
        if (broadcast || (request_length != 8U)) {
            return 0U;
        }
        const uint16_t start = ReadU16(&request[2]);
        const uint16_t count = ReadU16(&request[4]);
        if ((count == 0U) || (count > 125U) ||
            (response_capacity < (size_t)(5U + 2U * count))) {
            return ExceptionResponse(slave_address, function, MB_ILLEGAL_VALUE,
                                     false, response, response_capacity);
        }
        response[0] = slave_address;
        response[1] = function;
        response[2] = (uint8_t)(2U * count);
        /* 逐项读取寄存器，任意地址非法则返回异常帧。 */
        for (uint16_t index = 0U; index < count; ++index) {
            uint16_t value;
            const MbResult result = HostRegisters_Read(
                registers, (uint16_t)(start + index), &value);
            if (result != MB_OK) {
                return ExceptionResponse(slave_address, function, result,
                                         false, response, response_capacity);
            }
            WriteU16(&response[3U + 2U * index], value);
        }
        return FinishFrame(response, (size_t)(3U + 2U * count));
    }

    /* 功能码 0x06：写单个保持寄存器，正常响应回显原请求。 */
    if (function == 0x06U) {
        if (request_length != 8U) {
            return 0U;
        }
        const uint16_t address = ReadU16(&request[2]);
        const uint16_t value = ReadU16(&request[4]);
        const MbResult result = HostRegisters_WriteBatch(registers, address, &value, 1U);
        if (result != MB_OK) {
            return ExceptionResponse(slave_address, function, result,
                                     broadcast, response, response_capacity);
        }
        if (broadcast || response_capacity < 8U) {
            return 0U;
        }
        memcpy(response, request, 6U);
        return FinishFrame(response, 6U);
    }

    /* 功能码 0x10：一次原子写入多个连续寄存器。 */
    if (function == 0x10U) {
        if (request_length < 9U) {
            return 0U;
        }
        const uint16_t start = ReadU16(&request[2]);
        const uint16_t count = ReadU16(&request[4]);
        const uint8_t byte_count = request[6];
        if ((count == 0U) || (count > 10U) || (byte_count != 2U * count) ||
            (request_length != (size_t)(9U + byte_count))) {
            return ExceptionResponse(slave_address, function, MB_ILLEGAL_VALUE,
                                     broadcast, response, response_capacity);
        }
        uint16_t values[10]; /* 可写配置区共有 10 个常规寄存器。 */
        for (uint16_t index = 0U; index < count; ++index) {
            values[index] = ReadU16(&request[7U + 2U * index]);
        }
        const MbResult result = HostRegisters_WriteBatch(registers, start, values, count);
        if (result != MB_OK) {
            return ExceptionResponse(slave_address, function, result,
                                     broadcast, response, response_capacity);
        }
        if (broadcast || response_capacity < 8U) {
            return 0U;
        }
        memcpy(response, request, 6U);
        return FinishFrame(response, 6U);
    }

    /* 未实现的功能码返回“非法功能”异常。 */
    return ExceptionResponse(slave_address, function, MB_ILLEGAL_FUNCTION,
                             broadcast, response, response_capacity);
}
