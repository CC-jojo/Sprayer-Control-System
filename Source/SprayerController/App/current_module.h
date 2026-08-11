#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef enum
{
    CURRENT_WRITE_PENDING,  /* 等待发送目标电流写命令。 */
    CURRENT_WAIT_WRITE,     /* 已发送写命令，等待模块响应。 */
    CURRENT_VERIFY_PENDING, /* 等待发送寄存器回读命令。 */
    CURRENT_WAIT_VERIFY,    /* 已发送回读命令，等待模块响应。 */
    CURRENT_ONLINE,         /* 写入及回读一致，模块在线。 */
    CURRENT_RETRY_WAIT      /* 通信失败，正在等待下一次重试。 */
} CurrentModuleState;

typedef struct
{
    uint8_t slave_address;          /* 电流输出模块的 Modbus 从站地址。 */
    uint16_t target;                /* 目标电流，单位为微安，范围 0~20000。 */
    uint16_t readback;              /* 最近一次回读到的寄存器值。 */
    uint16_t fault_count;           /* 通信超时累计次数。 */
    uint16_t retry_remaining_ms;    /* 距离下一次重试的剩余毫秒数。 */
    CurrentModuleState state;       /* 当前通信状态机状态。 */
} CurrentModule;

/* 初始化电流模块通信状态。 */
void CurrentModule_Init(CurrentModule *module, uint8_t slave_address);
/* 更新0~20000微安目标电流，并安排发送写寄存器命令。 */
void CurrentModule_SetTargetMicroamps(CurrentModule *module, uint16_t target_microamps);
/* 在线状态下主动安排一次回读校验。 */
void CurrentModule_RequestVerify(CurrentModule *module);
/* 根据当前状态构造下一帧 Modbus RTU 请求。 */
size_t CurrentModule_BuildRequest(CurrentModule *module, uint8_t *request, size_t capacity);
/* 校验并处理电流模块响应，成功时推进状态机。 */
bool CurrentModule_OnResponse(CurrentModule *module, const uint8_t *response, size_t length);
/* 通信超时时累计故障，并进入 500 ms 重试等待。 */
void CurrentModule_OnTimeout(CurrentModule *module);
/* 推进重试等待计时。 */
void CurrentModule_Tick(CurrentModule *module, uint16_t elapsed_ms);
