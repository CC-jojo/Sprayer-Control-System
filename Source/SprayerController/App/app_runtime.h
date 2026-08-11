#pragma once
#include "current_module.h"
#include "host_registers.h"
#include "pwm_engine.h"
#include <stddef.h>
#include <stdint.h>

typedef struct {
    HostRegisters host;
    PwmEngine pwm;
    CurrentModule current_module;
    uint8_t host_slave_address;
    bool host_activity_level;
    uint16_t communication_ticks;
    uint8_t last_output_mask;
} AppRuntime;

void AppRuntime_Init(AppRuntime *runtime,uint8_t host_slave_address,uint8_t current_module_address);
void AppRuntime_LoadConfig(AppRuntime *runtime,const ControllerConfig *config);
uint8_t AppRuntime_Tick500us(AppRuntime *runtime);
size_t AppRuntime_ProcessHostFrame(AppRuntime *runtime,const uint8_t *request,size_t request_length,uint8_t *response,size_t response_capacity);
void AppRuntime_RefreshStatus(AppRuntime *runtime);
