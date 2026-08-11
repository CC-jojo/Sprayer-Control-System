#pragma once

#include "host_registers.h"

#include <stddef.h>
#include <stdint.h>

/* 解析一帧上位机 Modbus RTU 请求；返回响应长度，广播或无效帧返回 0。 */
size_t HostModbus_HandleFrame(
    HostRegisters *registers,
    uint8_t slave_address,
    const uint8_t *request,
    size_t request_length,
    uint8_t *response,
    size_t response_capacity);
