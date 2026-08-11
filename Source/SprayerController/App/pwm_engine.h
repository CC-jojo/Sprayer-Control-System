#pragma once

#include <stdbool.h>
#include <stdint.h>

typedef struct
{
    uint16_t tick;           // 当前时间轴计数值 (0~199)
    uint8_t  active_mask;    // 当前正在生效的通道开关 (按位表示)
    uint8_t  pending_mask;   // 等待生效的通道开关 (缓冲)
    uint8_t  active_duty;    // 当前正在生效的占空比 (0~100)
    uint8_t  pending_duty;   // 等待生效的占空比 (缓冲)
    bool     pending_valid;  // 标记是否有新数据等待更新
} PwmEngine;

void PwmEngine_Init(PwmEngine *engine);
void PwmEngine_SetPending(PwmEngine *engine, uint8_t enable_mask, uint8_t duty_percent);
void PwmEngine_ResetPeriod(PwmEngine *engine);
uint8_t PwmEngine_Tick500us(PwmEngine *engine);
