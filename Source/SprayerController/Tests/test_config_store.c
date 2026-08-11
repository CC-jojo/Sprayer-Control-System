#include "config_store.h"

#include <assert.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

enum { RECORD_SIZE = 24U, CRC_OFFSET = 20U };

static void WriteU16(uint8_t *bytes, uint16_t value)
{
    bytes[0] = (uint8_t)value;
    bytes[1] = (uint8_t)(value >> 8U);
}

static void WriteU32(uint8_t *bytes, uint32_t value)
{
    bytes[0] = (uint8_t)value;
    bytes[1] = (uint8_t)(value >> 8U);
    bytes[2] = (uint8_t)(value >> 16U);
    bytes[3] = (uint8_t)(value >> 24U);
}

static uint32_t Crc32(const uint8_t *data, size_t length)
{
    uint32_t crc = 0xFFFFFFFFU;
    for (size_t index = 0U; index < length; ++index) {
        crc ^= data[index];
        for (uint8_t bit = 0U; bit < 8U; ++bit) {
            crc = (crc & 1U) != 0U ? (crc >> 1U) ^ 0xEDB88320U : crc >> 1U;
        }
    }
    return crc ^ 0xFFFFFFFFU;
}

static void FinalizeRecord(uint8_t *record)
{
    WriteU32(record + CRC_OFFSET, Crc32(record, CRC_OFFSET));
}

static void AssertDefaults(const ControllerConfig *config)
{
    assert(config->mode == SPRAY_ALL);
    assert(config->nozzle_mask == 0xFFU);
    assert(config->duty == 30U);
    assert(config->duty_min == 30U);
    assert(config->duty_max == 80U);
    assert(!config->persistence_enabled);
    assert(config->target_current_ua == 0U);
}

static ControllerConfig CustomConfig(uint8_t mask)
{
    ControllerConfig config;
    ControllerConfig_SetDefaults(&config);
    config.mode = SPRAY_CUSTOM;
    config.nozzle_mask = mask;
    config.duty = 42U;
    config.persistence_enabled = true;
    config.target_current_ua = 12345U;
    return config;
}

static void TestV4LayoutAndLegacyRoundTrips(void)
{
    static const uint8_t masks[] = { 0x00U, 0xC0U, 0xF0U, 0xFCU, 0xFFU };
    uint8_t record[RECORD_SIZE];

    for (uint16_t mode = SPRAY_ALL_OFF; mode <= SPRAY_ALL; ++mode) {
        ControllerConfig encoded;
        ControllerConfig decoded;
        ControllerConfig_SetDefaults(&encoded);
        encoded.mode = (SprayMode)mode;
        encoded.nozzle_mask = masks[mode];
        encoded.persistence_enabled = true;
        assert(ConfigStore_Encode(&encoded, record, sizeof(record)));
        assert(record[19] == masks[mode]);
        assert(ConfigStore_Decode(record, sizeof(record), &decoded));
        assert(decoded.mode == (SprayMode)mode);
        assert(decoded.nozzle_mask == masks[mode]);
    }

    ControllerConfig config = CustomConfig(0xA5U);
    assert(CONFIG_STORE_RECORD_SIZE == RECORD_SIZE);
    assert(ConfigStore_Encode(&config, record, sizeof(record)));
    assert(record[0] == 0x59U && record[1] == 0x52U && record[2] == 0x50U && record[3] == 0x53U);
    assert(record[4] == 4U && record[5] == 0U);
    assert(record[6] == 12U && record[7] == 0U);
    assert(record[8] == SPRAY_CUSTOM && record[9] == 42U);
    assert(record[14] == 30U && record[15] == 80U && record[16] == 1U);
    assert(record[17] == 0x39U && record[18] == 0x30U);
    assert(record[19] == 0xA5U);
    assert(ConfigStore_Decode(record, sizeof(record), &config));
    assert(config.mode == SPRAY_CUSTOM && config.nozzle_mask == 0xA5U);
}

static void TestV4CustomMaskEdges(void)
{
    static const uint8_t masks[] = { 0x00U, 0xA5U, 0xFFU };
    uint8_t record[RECORD_SIZE];

    for (uint8_t index = 0U; index < (uint8_t)(sizeof(masks) / sizeof(masks[0])); ++index) {
        ControllerConfig encoded = CustomConfig(masks[index]);
        ControllerConfig decoded;
        assert(ConfigStore_Encode(&encoded, record, sizeof(record)));
        assert(ConfigStore_Decode(record, sizeof(record), &decoded));
        assert(decoded.mode == SPRAY_CUSTOM);
        assert(decoded.nozzle_mask == masks[index]);
    }
}

static void TestHandcraftedV3DerivesLegacyMask(void)
{
    uint8_t record[RECORD_SIZE] = { 0U };
    ControllerConfig decoded;
    WriteU32(record, 0x53505259U);
    WriteU16(record + 4U, 3U);
    WriteU16(record + 6U, 12U);
    record[8] = SPRAY_GROUP234;
    record[9] = 37U;
    record[14] = 30U;
    record[15] = 80U;
    record[16] = 1U;
    WriteU16(record + 17U, 12000U);
    record[19] = 0xA5U;
    FinalizeRecord(record);

    assert(ConfigStore_Decode(record, sizeof(record), &decoded));
    assert(decoded.mode == SPRAY_GROUP234);
    assert(decoded.nozzle_mask == 0xFCU);
    assert(decoded.duty == 37U);
}

static void TestDecodeFailuresRestoreCompleteDefaults(void)
{
    uint8_t record[RECORD_SIZE];
    ControllerConfig config;
    const ControllerConfig valid = CustomConfig(0x55U);
    assert(ConfigStore_Encode(&valid, record, sizeof(record)));

    record[0] ^= 1U;
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);
    record[0] ^= 1U;

    record[20] ^= 1U;
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);
    record[20] ^= 1U;

    WriteU16(record + 4U, 5U);
    FinalizeRecord(record);
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);

    WriteU16(record + 4U, 4U);
    WriteU16(record + 6U, 11U);
    FinalizeRecord(record);
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);

    WriteU16(record + 6U, 12U);
    record[16] = 2U;
    FinalizeRecord(record);
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);

    record[16] = 1U;
    record[8] = 6U;
    FinalizeRecord(record);
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);

    ControllerConfig_SetDefaults(&config);
    config.mode = SPRAY_GROUP4;
    config.nozzle_mask = 0xC0U;
    assert(ConfigStore_Encode(&config, record, sizeof(record)));
    record[19] = 0xFFU;
    FinalizeRecord(record);
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);

    memset(record, 0, sizeof(record));
    WriteU32(record, 0x53505259U);
    WriteU16(record + 4U, 3U);
    WriteU16(record + 6U, 12U);
    record[8] = SPRAY_CUSTOM;
    record[9] = 30U;
    record[14] = 30U;
    record[15] = 80U;
    record[16] = 1U;
    FinalizeRecord(record);
    assert(!ConfigStore_Decode(record, sizeof(record), &config));
    AssertDefaults(&config);

    assert(!ConfigStore_Decode(record, RECORD_SIZE - 1U, &config));
    AssertDefaults(&config);
}

int main(void)
{
    TestV4LayoutAndLegacyRoundTrips();
    TestV4CustomMaskEdges();
    TestHandcraftedV3DerivesLegacyMask();
    TestDecodeFailuresRestoreCompleteDefaults();
    return 0;
}
