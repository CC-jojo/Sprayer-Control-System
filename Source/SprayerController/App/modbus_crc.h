#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* 计算 Modbus RTU 使用的 CRC-16（多项式 0xA001）。 */
uint16_t Modbus_Crc16(const uint8_t *data, size_t length);
/* 检查帧尾两个字节是否与计算所得 CRC 一致。 */
bool Modbus_FrameHasValidCrc(const uint8_t *frame, size_t length);
