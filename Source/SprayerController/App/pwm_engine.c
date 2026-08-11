#include "pwm_engine.h"

enum
{
    PWM_CHANNEL_COUNT = 8,
    PWM_PERIOD_TICKS = 200,
    PWM_DEFAULT_DUTY = 60   //上电默认占空比
};

/* Bit order: L1, R1, L2, R2, L3, R3, L4, R4. */
static const uint16_t phase_ticks[PWM_CHANNEL_COUNT] = {
    75U, 175U, 25U, 125U, 50U, 150U, 0U, 100U
};

void PwmEngine_Init(PwmEngine *engine)
{
    engine->tick = 0U;
    engine->active_mask = 0U;
    engine->pending_mask = 0U;
    engine->active_duty = PWM_DEFAULT_DUTY;
    engine->pending_duty = PWM_DEFAULT_DUTY;
    engine->pending_valid = false;
}

void PwmEngine_SetPending(PwmEngine *engine, uint8_t enable_mask, uint8_t duty_percent)
{
    engine->pending_mask = enable_mask;
    engine->pending_duty = duty_percent;
    engine->pending_valid = true;
}

void PwmEngine_ResetPeriod(PwmEngine *engine)
{
    engine->tick = 0U;
}

uint8_t PwmEngine_Tick500us(PwmEngine *engine)
{
    uint8_t output_mask = 0U;

    if ((engine->tick == 0U) && engine->pending_valid) {
        engine->active_mask = engine->pending_mask;
        engine->active_duty = engine->pending_duty;
        engine->pending_valid = false;
    }

    const uint16_t high_ticks = (uint16_t)engine->active_duty * 2U;
    for (uint8_t channel = 0U; channel < PWM_CHANNEL_COUNT; ++channel) {
        const uint16_t relative =
            (uint16_t)((engine->tick + PWM_PERIOD_TICKS - phase_ticks[channel]) % PWM_PERIOD_TICKS);
        const uint8_t bit = (uint8_t)(1U << channel);
        if (((engine->active_mask & bit) != 0U) && (relative < high_ticks)) {
            output_mask |= bit;
        }
    }

    engine->tick = (uint16_t)((engine->tick + 1U) % PWM_PERIOD_TICKS);
    return output_mask;
}
