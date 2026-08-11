#include "modbus_crc.h"

/* 按 Modbus RTU 标准逐字节、逐位更新 CRC。 */
uint16_t Modbus_Crc16(const uint8_t *data, size_t length)
{
    uint16_t crc = 0xFFFFU;
    for (size_t index = 0U; index < length; ++index) {
        crc ^= data[index];
        /* 每个输入字节依次处理 8 个最低位。 */
        for (uint8_t bit = 0U; bit < 8U; ++bit) {
            if ((crc & 1U) != 0U) {
                crc = (uint16_t)((crc >> 1U) ^ 0xA001U);
            } else {
                crc >>= 1U;
            }
        }
    }
    return crc;
}

/* Modbus 在线路上先发送 CRC 低字节，再发送高字节。 */
bool Modbus_FrameHasValidCrc(const uint8_t *frame, size_t length)
{
    if (length < 4U) {
        return false;
    }
    const uint16_t received =
        (uint16_t)frame[length - 2U] | ((uint16_t)frame[length - 1U] << 8U);
    return Modbus_Crc16(frame, length - 2U) == received;
}
