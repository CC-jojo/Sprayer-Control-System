#include "app_runtime.h"
#include "host_modbus.h"

enum { COMMUNICATION_TIMEOUT_TICKS = 4000U };

static void ApplyTargets(AppRuntime *r)
{
    PwmEngine_SetPending(&r->pwm,HostRegisters_GetOutputMask(&r->host),r->host.config.duty);
    const uint16_t target=r->host.config.target_current_ua;
    if(target!=r->current_module.target)CurrentModule_SetTargetMicroamps(&r->current_module,target);
}

void AppRuntime_Init(AppRuntime *r,uint8_t host_addr,uint8_t current_addr)
{
    HostRegisters_Init(&r->host); PwmEngine_Init(&r->pwm); CurrentModule_Init(&r->current_module,current_addr);
    r->host_slave_address=host_addr; r->host_activity_level=false; r->communication_ticks=0U; r->last_output_mask=0U;
    r->host.status.firmware_version=0x0202U; ApplyTargets(r); AppRuntime_RefreshStatus(r);
}

void AppRuntime_LoadConfig(AppRuntime *r,const ControllerConfig *c)
{ if(ControllerConfig_IsValid(c)){r->host.config=*c;ApplyTargets(r);AppRuntime_RefreshStatus(r);} }

uint8_t AppRuntime_Tick500us(AppRuntime *r)
{
    const uint8_t calculated=PwmEngine_Tick500us(&r->pwm);
    if(r->communication_ticks<COMMUNICATION_TIMEOUT_TICKS)r->communication_ticks++;
    if(r->communication_ticks>=COMMUNICATION_TIMEOUT_TICKS)r->host.status.communication_stopped=1U;
    r->last_output_mask=r->host.pwm_enabled?calculated:0U;
    AppRuntime_RefreshStatus(r); return r->last_output_mask;
}

size_t AppRuntime_ProcessHostFrame(AppRuntime *r,const uint8_t *request,size_t request_length,uint8_t *response,size_t capacity)
{
    const size_t n=HostModbus_HandleFrame(&r->host,r->host_slave_address,request,request_length,response,capacity);
    if(n>=2U && (response[1]&0x80U)==0U){
        r->host_activity_level=!r->host_activity_level; r->communication_ticks=0U; r->host.status.communication_stopped=0U;
    }
    ApplyTargets(r); AppRuntime_RefreshStatus(r); return n;
}

void AppRuntime_RefreshStatus(AppRuntime *r)
{
    r->host.status.pwm_tick=r->pwm.tick;
    /* 0x0108 上报“当前配置的有效输出掩码”（稳定值），而非 PWM 每一瞬间的电平。
       last_output_mask 是 10Hz PWM 实时输出，若上报会让上位机界面开关状态乱跳。 */
    r->host.status.output_mask=r->host.pwm_enabled?HostRegisters_GetOutputMask(&r->host):0U;
    r->host.status.current_module_state=(r->current_module.state==CURRENT_ONLINE)?1U:(r->current_module.state==CURRENT_RETRY_WAIT)?2U:0U;
    r->host.status.target_current_ua=r->current_module.target; r->host.status.current_module_readback=r->current_module.readback;
    r->host.status.current_module_fault_count=r->current_module.fault_count;
}
