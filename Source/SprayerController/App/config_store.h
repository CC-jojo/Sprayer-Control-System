#pragma once

#include "host_registers.h"

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#define CONFIG_STORE_RECORD_SIZE 24U /* Flash半字写入要求记录长度为偶数。 */

/* 将合法配置编码成带版本号和CRC32的24字节记录。 */
bool ConfigStore_Encode(
    const ControllerConfig *config,
    uint8_t *record,
    size_t record_size);
/* 校验并解码配置记录；失败时输出默认配置并返回 false。 */
bool ConfigStore_Decode(
    const uint8_t *record,
    size_t record_size,
    ControllerConfig *config);
