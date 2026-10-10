#pragma once

#include <stdint.h>

#define APP_HOST_BAUD_RATE      38400U
#define APP_PWM_CHANNELS        8U
#define APP_PWM_PERIOD_TICKS    200U /* 0.5 ms x 200 = 100 ms (10 Hz). */
#define APP_PWM_MIN_DEFAULT     30U
#define APP_PWM_MAX_DEFAULT     80U
#define APP_CURRENT_MAX_UA      20000U /* 电流模块量程上限：20.000 mA。 */
