#include "host_registers.h"
#include "app_runtime.h"
#include "config_store.h"
#include "host_modbus.h"
#include "modbus_crc.h"

#include <assert.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>

static void ExpectRead(const HostRegisters *registers, uint16_t address, uint16_t expected)
{
    uint16_t value = UINT16_MAX;
    assert(HostRegisters_Read(registers, address, &value) == MB_OK);
    assert(value == expected);
}

static bool ConfigEquals(const ControllerConfig *left, const ControllerConfig *right)
{
    return left->mode == right->mode &&
           left->nozzle_mask == right->nozzle_mask &&
           left->duty == right->duty &&
           left->duty_min == right->duty_min &&
           left->duty_max == right->duty_max &&
           left->persistence_enabled == right->persistence_enabled &&
           left->target_current_ua == right->target_current_ua;
}

static size_t AppendCrc(uint8_t *frame, size_t payload_length)
{
    const uint16_t crc = Modbus_Crc16(frame, payload_length);
    frame[payload_length] = (uint8_t)(crc & 0xFFU);
    frame[payload_length + 1U] = (uint8_t)(crc >> 8U);
    return payload_length + 2U;
}

static void AssertValidResponse(const uint8_t *response, size_t response_length)
{
    assert(response_length >= 5U);
    assert(Modbus_FrameHasValidCrc(response, response_length));
}

static void TestDefaultsAndExistingRegisters(void)
{
    HostRegisters registers;
    HostRegisters_Init(&registers);

    assert(registers.config.mode == SPRAY_ALL);
    assert(registers.config.nozzle_mask == 0xFFU);
    assert(registers.config.duty == 30U);
    assert(registers.config.duty_min == 30U);
    assert(registers.config.duty_max == 80U);
    assert(!registers.config.persistence_enabled);
    assert(registers.config.target_current_ua == 0U);
    assert(HostRegisters_GetOutputMask(&registers) == 0xFFU);

    ExpectRead(&registers, 0x0000U, SPRAY_ALL);
    ExpectRead(&registers, 0x0001U, 30U);
    ExpectRead(&registers, 0x0006U, 30U);
    ExpectRead(&registers, 0x0007U, 80U);
    ExpectRead(&registers, 0x0008U, 0U);
    ExpectRead(&registers, 0x0009U, 0U);
    ExpectRead(&registers, 0x000BU, 1U);
}

static void TestLegacyModesAlwaysDeriveTheirMasks(void)
{
    static const uint8_t expected_masks[] = { 0x00U, 0xC0U, 0xF0U, 0xFCU, 0xFFU };
    HostRegisters registers;
    HostRegisters_Init(&registers);

    for (uint16_t mode = SPRAY_ALL_OFF; mode <= SPRAY_ALL; ++mode) {
        const uint16_t value = mode;
        assert(HostRegisters_WriteBatch(&registers, 0x0000U, &value, 1U) == MB_OK);
        assert(registers.config.mode == (SprayMode)mode);
        assert(registers.config.nozzle_mask == expected_masks[mode]);
        assert(HostRegisters_GetOutputMask(&registers) == expected_masks[mode]);
    }
}

static void TestExplicitMaskWriteReadAndEdges(void)
{
    static const uint16_t masks[] = { 0x0000U, 0x00A5U, 0x00FFU };
    HostRegisters registers;
    HostRegisters_Init(&registers);

    for (uint8_t index = 0U; index < (uint8_t)(sizeof(masks) / sizeof(masks[0])); ++index) {
        const uint16_t mask = masks[index];
        assert(HostRegisters_WriteBatch(&registers, 0x000CU, &mask, 1U) == MB_OK);
        assert(registers.config.mode == SPRAY_CUSTOM);
        assert(registers.config.nozzle_mask == (uint8_t)mask);
        assert(HostRegisters_GetOutputMask(&registers) == (uint8_t)mask);
        ExpectRead(&registers, 0x000CU, mask);
    }
}

static void TestInvalidMaskIsAtomic(void)
{
    HostRegisters registers;
    HostRegisters_Init(&registers);
    const uint16_t valid_mask = 0x00A5U;
    const uint16_t invalid_mask = 0x0100U;
    assert(HostRegisters_WriteBatch(&registers, 0x000CU, &valid_mask, 1U) == MB_OK);
    const ControllerConfig before = registers.config;

    assert(HostRegisters_WriteBatch(&registers, 0x000CU, &invalid_mask, 1U) == MB_ILLEGAL_VALUE);
    assert(ConfigEquals(&registers.config, &before));
}

static void TestModeMaskSynchronizationAndCapabilities(void)
{
    HostRegisters registers;
    HostRegisters_Init(&registers);
    const uint16_t custom_mask = 0x0055U;
    const uint16_t custom_mode = SPRAY_CUSTOM;
    const uint16_t group34_mode = SPRAY_GROUP34;

    assert(HostRegisters_WriteBatch(&registers, 0x000CU, &custom_mask, 1U) == MB_OK);
    assert(HostRegisters_WriteBatch(&registers, 0x0000U, &custom_mode, 1U) == MB_OK);
    assert(registers.config.mode == SPRAY_CUSTOM);
    assert(registers.config.nozzle_mask == 0x55U);
    assert(HostRegisters_WriteBatch(&registers, 0x0000U, &group34_mode, 1U) == MB_OK);
    assert(registers.config.mode == SPRAY_GROUP34);
    assert(registers.config.nozzle_mask == 0xF0U);

    ExpectRead(&registers, 0x010AU, 2U);
    ExpectRead(&registers, 0x010BU, 0x0001U);
}

static void TestExistingWritableRegistersRemainCompatible(void)
{
    HostRegisters registers;
    HostRegisters_Init(&registers);
    const uint16_t values[] = { 35U, 0U, 0U, 0U, 0U, 30U, 80U, 1U, 20000U };
    const uint16_t save_key = 0xA55AU;
    const uint16_t pwm_off = 0U;

    assert(HostRegisters_WriteBatch(&registers, 0x0001U, values, 9U) == MB_OK);
    assert(registers.config.duty == 35U);
    assert(registers.config.persistence_enabled);
    assert(registers.config.target_current_ua == 20000U);
    assert(HostRegisters_WriteBatch(&registers, 0x000AU, &save_key, 1U) == MB_OK);
    assert(registers.save_requested);
    assert(HostRegisters_WriteBatch(&registers, 0x000BU, &pwm_off, 1U) == MB_OK);
    ExpectRead(&registers, 0x000BU, 0U);
}

static void TestRuntimeReportsVersionAndPwmGatedCustomOutput(void)
{
    AppRuntime runtime;
    ControllerConfig config;
    AppRuntime_Init(&runtime, 1U, 1U);
    ExpectRead(&runtime.host, 0x0106U, 0x0202U);

    ControllerConfig_SetDefaults(&config);
    config.mode = SPRAY_CUSTOM;
    config.nozzle_mask = 0x40U;
    AppRuntime_LoadConfig(&runtime, &config);
    PwmEngine_ResetPeriod(&runtime.pwm);
    assert(AppRuntime_Tick500us(&runtime) == 0x40U);
    ExpectRead(&runtime.host, 0x0108U, 0x40U);

    runtime.host.pwm_enabled = false;
    PwmEngine_ResetPeriod(&runtime.pwm);
    assert(AppRuntime_Tick500us(&runtime) == 0U);
    ExpectRead(&runtime.host, 0x0108U, 0U);

    runtime.host.pwm_enabled = true;
    config.nozzle_mask = 0x00U;
    AppRuntime_LoadConfig(&runtime, &config);
    PwmEngine_ResetPeriod(&runtime.pwm);
    assert(AppRuntime_Tick500us(&runtime) == 0U);
    ExpectRead(&runtime.host, 0x0108U, 0U);
}

static void TestInvalidModesAreRejectedAndUseSafeFallback(void)
{
    static const SprayMode invalid_modes[] = {
        (SprayMode)-1,
        (SprayMode)(SPRAY_CUSTOM + 1)
    };
    uint8_t record[CONFIG_STORE_RECORD_SIZE];

    for (uint8_t index = 0U; index < (uint8_t)(sizeof(invalid_modes) / sizeof(invalid_modes[0])); ++index) {
        ControllerConfig config;
        ControllerConfig_SetDefaults(&config);
        config.mode = invalid_modes[index];
        config.nozzle_mask = 0U;
        assert(ControllerConfig_GetLegacyMask(config.mode) == 0U);
        assert(!ControllerConfig_IsValid(&config));
        assert(HostRegisters_GetOutputMask(&(HostRegisters){ .config = config }) == 0U);
        assert(!ConfigStore_Encode(&config, record, sizeof(record)));
    }
}

static void TestExplicitMaskWireFrames(void)
{
    HostRegisters registers;
    uint8_t request[16];
    uint8_t response[16];
    size_t request_length;
    size_t response_length;

    HostRegisters_Init(&registers);

    const uint8_t write_single_payload[] = { 1U, 0x06U, 0x00U, 0x0CU, 0x00U, 0xA5U };
    memcpy(request, write_single_payload, sizeof(write_single_payload));
    request_length = AppendCrc(request, sizeof(write_single_payload));
    response_length = HostModbus_HandleFrame(&registers, 1U, request, request_length, response, sizeof(response));
    AssertValidResponse(response, response_length);
    assert(response_length == request_length);
    assert(memcmp(response, request, request_length) == 0);
    assert(registers.config.mode == SPRAY_CUSTOM && registers.config.nozzle_mask == 0xA5U);

    const uint8_t write_multiple_payload[] = { 1U, 0x10U, 0x00U, 0x0CU, 0x00U, 0x01U, 0x02U, 0x00U, 0x5AU };
    memcpy(request, write_multiple_payload, sizeof(write_multiple_payload));
    request_length = AppendCrc(request, sizeof(write_multiple_payload));
    response_length = HostModbus_HandleFrame(&registers, 1U, request, request_length, response, sizeof(response));
    AssertValidResponse(response, response_length);
    assert(response_length == 8U);
    assert(response[0] == 1U && response[1] == 0x10U && response[2] == 0U && response[3] == 0x0CU);
    assert(response[4] == 0U && response[5] == 1U);
    assert(registers.config.mode == SPRAY_CUSTOM && registers.config.nozzle_mask == 0x5AU);

    const uint8_t read_mask_payload[] = { 1U, 0x03U, 0x00U, 0x0CU, 0x00U, 0x01U };
    memcpy(request, read_mask_payload, sizeof(read_mask_payload));
    request_length = AppendCrc(request, sizeof(read_mask_payload));
    response_length = HostModbus_HandleFrame(&registers, 1U, request, request_length, response, sizeof(response));
    AssertValidResponse(response, response_length);
    assert(response_length == 7U);
    assert(response[0] == 1U && response[1] == 0x03U && response[2] == 2U);
    assert(response[3] == 0U && response[4] == 0x5AU);

    const uint8_t read_capabilities_payload[] = { 1U, 0x03U, 0x01U, 0x0AU, 0x00U, 0x02U };
    memcpy(request, read_capabilities_payload, sizeof(read_capabilities_payload));
    request_length = AppendCrc(request, sizeof(read_capabilities_payload));
    response_length = HostModbus_HandleFrame(&registers, 1U, request, request_length, response, sizeof(response));
    AssertValidResponse(response, response_length);
    assert(response_length == 9U);
    assert(response[0] == 1U && response[1] == 0x03U && response[2] == 4U);
    assert(response[3] == 0U && response[4] == 2U && response[5] == 0U && response[6] == 1U);

    const ControllerConfig before = registers.config;
    const uint8_t invalid_mask_payload[] = { 1U, 0x06U, 0x00U, 0x0CU, 0x01U, 0x00U };
    memcpy(request, invalid_mask_payload, sizeof(invalid_mask_payload));
    request_length = AppendCrc(request, sizeof(invalid_mask_payload));
    response_length = HostModbus_HandleFrame(&registers, 1U, request, request_length, response, sizeof(response));
    AssertValidResponse(response, response_length);
    assert(response_length == 5U);
    assert(response[0] == 1U && response[1] == 0x86U && response[2] == MB_ILLEGAL_VALUE);
    assert(ConfigEquals(&registers.config, &before));
}

int main(void)
{
    TestDefaultsAndExistingRegisters();
    TestLegacyModesAlwaysDeriveTheirMasks();
    TestExplicitMaskWriteReadAndEdges();
    TestInvalidMaskIsAtomic();
    TestModeMaskSynchronizationAndCapabilities();
    TestExistingWritableRegistersRemainCompatible();
    TestRuntimeReportsVersionAndPwmGatedCustomOutput();
    TestInvalidModesAreRejectedAndUseSafeFallback();
    TestExplicitMaskWireFrames();
    return 0;
}
