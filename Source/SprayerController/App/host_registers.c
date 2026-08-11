#include "host_registers.h"
#include <string.h>

bool ControllerConfig_IsValid(const ControllerConfig *c)
{
    return c->mode <= SPRAY_ALL && c->duty_min == 30U && c->duty_max == 80U &&
           c->duty >= c->duty_min && c->duty <= c->duty_max && c->target_current_ua <= 20000U;
}

void ControllerConfig_SetDefaults(ControllerConfig *c)
{
    c->mode=SPRAY_ALL; c->duty=30U; c->duty_min=30U; c->duty_max=80U;
    c->persistence_enabled=false; c->target_current_ua=0U;
}

void HostRegisters_Init(HostRegisters *r)
{
    ControllerConfig_SetDefaults(&r->config); memset(&r->status,0,sizeof(r->status));
    r->pwm_enabled=true; r->save_requested=false;
}

uint8_t HostRegisters_GetOutputMask(const HostRegisters *r)
{
    static const uint8_t masks[5]={0x00U,0xC0U,0xF0U,0xFCU,0xFFU};
    return masks[r->config.mode];
}

MbResult HostRegisters_WriteBatch(HostRegisters *r,uint16_t start,const uint16_t *v,uint16_t count)
{
    ControllerConfig c=r->config;
    if(start==0x000AU){ if(count!=1U||v[0]!=0xA55AU||!r->config.persistence_enabled)return MB_ILLEGAL_VALUE; r->save_requested=true; return MB_OK; }
    if(start==0x000BU){ if(count!=1U||v[0]>1U)return MB_ILLEGAL_VALUE; r->pwm_enabled=v[0]!=0U; return MB_OK; }
    if(count==0U || (uint32_t)start+count>0x000AU)return MB_ILLEGAL_ADDRESS;
    for(uint16_t i=0;i<count;i++){
        uint16_t a=(uint16_t)(start+i), x=v[i];
        switch(a){
        case 0x0000U:if(x>255U)return MB_ILLEGAL_VALUE;c.mode=(SprayMode)x;break;
        case 0x0001U:if(x>255U)return MB_ILLEGAL_VALUE;c.duty=(uint8_t)x;break;
        case 0x0002U:case 0x0003U:case 0x0004U:case 0x0005U:if(x!=0U)return MB_ILLEGAL_VALUE;break;
        case 0x0006U:if(x!=30U)return MB_ILLEGAL_VALUE;c.duty_min=30U;break;
        case 0x0007U:if(x!=80U)return MB_ILLEGAL_VALUE;c.duty_max=80U;break;
        case 0x0008U:if(x>1U)return MB_ILLEGAL_VALUE;c.persistence_enabled=x!=0U;break;
        case 0x0009U:if(x>20000U)return MB_ILLEGAL_VALUE;c.target_current_ua=x;break;
        default:return MB_ILLEGAL_ADDRESS;
        }
    }
    if(!ControllerConfig_IsValid(&c)) return MB_ILLEGAL_VALUE;
    r->config=c;
    return MB_OK;
}

MbResult HostRegisters_Read(const HostRegisters *r,uint16_t a,uint16_t *v)
{
    switch(a){
    case 0x0000U:*v=r->config.mode;break;case 0x0001U:*v=r->config.duty;break;
    case 0x0002U:case 0x0003U:case 0x0004U:case 0x0005U:*v=0U;break;
    case 0x0006U:*v=30U;break;case 0x0007U:*v=80U;break;case 0x0008U:*v=r->config.persistence_enabled;break;
    case 0x0009U:*v=r->config.target_current_ua;break;case 0x000AU:*v=0U;break;case 0x000BU:*v=r->pwm_enabled;break;
    case 0x0100U:*v=r->status.pwm_tick;break;case 0x0101U:*v=r->status.current_module_state;break;
    case 0x0102U:*v=r->status.target_current_ua;break;case 0x0103U:*v=r->status.current_module_readback;break;
    case 0x0104U:*v=r->status.current_module_fault_count;break;case 0x0105U:*v=r->status.last_parameter_error;break;
    case 0x0106U:*v=r->status.firmware_version;break;case 0x0107U:*v=r->pwm_enabled;break;
    case 0x0108U:*v=r->status.output_mask;break;case 0x0109U:*v=r->status.communication_stopped;break;
    default:return MB_ILLEGAL_ADDRESS;} return MB_OK;
}
