#include "config_store.h"

enum { CONFIG_MAGIC=0x53505259U, CONFIG_VERSION=3U, CONFIG_PAYLOAD_SIZE=12U, CONFIG_CRC_OFFSET=20U };
static void w16(uint8_t*b,uint16_t v){b[0]=(uint8_t)v;b[1]=(uint8_t)(v>>8U);}
static uint16_t r16(const uint8_t*b){return(uint16_t)(b[0]|((uint16_t)b[1]<<8U));}
static void w32(uint8_t*b,uint32_t v){b[0]=(uint8_t)v;b[1]=(uint8_t)(v>>8U);b[2]=(uint8_t)(v>>16U);b[3]=(uint8_t)(v>>24U);}
static uint32_t r32(const uint8_t*b){return(uint32_t)b[0]|((uint32_t)b[1]<<8U)|((uint32_t)b[2]<<16U)|((uint32_t)b[3]<<24U);}
static uint32_t crc32(const uint8_t*d,size_t n){uint32_t c=0xFFFFFFFFU;for(size_t i=0;i<n;i++){c^=d[i];for(uint8_t b=0;b<8;b++)c=(c&1U)?((c>>1U)^0xEDB88320U):(c>>1U);}return c^0xFFFFFFFFU;}

bool ConfigStore_Encode(const ControllerConfig*c,uint8_t*r,size_t size)
{
    if(size<CONFIG_STORE_RECORD_SIZE||!ControllerConfig_IsValid(c))return false;
    w32(r,CONFIG_MAGIC);w16(r+4,CONFIG_VERSION);w16(r+6,CONFIG_PAYLOAD_SIZE);
    r[8]=(uint8_t)c->mode;r[9]=c->duty;for(uint8_t i=0;i<4;i++)r[10+i]=0U;
    r[14]=30U;r[15]=80U;r[16]=c->persistence_enabled?1U:0U;w16(r+17,c->target_current_ua);r[19]=0U;
    w32(r+CONFIG_CRC_OFFSET,crc32(r,CONFIG_CRC_OFFSET));return true;
}

bool ConfigStore_Decode(const uint8_t*r,size_t size,ControllerConfig*c)
{
    bool ok=size>=CONFIG_STORE_RECORD_SIZE;
    if(ok)ok=r32(r)==CONFIG_MAGIC&&r16(r+4)==CONFIG_VERSION&&r16(r+6)==CONFIG_PAYLOAD_SIZE&&r32(r+CONFIG_CRC_OFFSET)==crc32(r,CONFIG_CRC_OFFSET);
    ControllerConfig x;
    if(ok){x.mode=(SprayMode)r[8];x.duty=r[9];x.duty_min=r[14];x.duty_max=r[15];x.persistence_enabled=r[16]!=0U;x.target_current_ua=r16(r+17);ok=r[16]<=1U&&ControllerConfig_IsValid(&x);}
    if(ok)*c=x;else ControllerConfig_SetDefaults(c);return ok;
}
