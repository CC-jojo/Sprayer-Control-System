#pragma once
#include <stdbool.h>
#include <stdint.h>

typedef enum { MB_OK=0, MB_ILLEGAL_FUNCTION=1, MB_ILLEGAL_ADDRESS=2, MB_ILLEGAL_VALUE=3 } MbResult;
typedef enum {
    SPRAY_ALL_OFF=0, SPRAY_GROUP4=1, SPRAY_GROUP34=2,
    SPRAY_GROUP234=3, SPRAY_ALL=4, SPRAY_CUSTOM=5
} SprayMode;

typedef struct {
    SprayMode mode;
    uint8_t nozzle_mask;
    uint8_t duty;
    uint8_t duty_min;
    uint8_t duty_max;
    bool persistence_enabled;
    uint16_t target_current_ua;
} ControllerConfig;

typedef struct {
    uint16_t pwm_tick, current_module_state, target_current_ua, current_module_readback;
    uint16_t current_module_fault_count, last_parameter_error, firmware_version;
    uint16_t output_mask, communication_stopped;
} ControllerStatus;

typedef struct {
    ControllerConfig config;
    ControllerStatus status;
    bool pwm_enabled;
    bool save_requested;
} HostRegisters;

void ControllerConfig_SetDefaults(ControllerConfig *config);
bool ControllerConfig_IsValid(const ControllerConfig *config);
/* Legacy modes return their fixed mask; Custom and invalid modes return safe disabled mask 0x00. */
uint8_t ControllerConfig_GetLegacyMask(SprayMode mode);
void HostRegisters_Init(HostRegisters *registers);
MbResult HostRegisters_WriteBatch(HostRegisters *registers, uint16_t start_address, const uint16_t *values, uint16_t count);
MbResult HostRegisters_Read(const HostRegisters *registers, uint16_t address, uint16_t *value);
uint8_t HostRegisters_GetOutputMask(const HostRegisters *registers);
