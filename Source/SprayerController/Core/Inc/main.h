/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.h
  * @brief          : Header for main.c file.
  *                   This file contains the common defines of the application.
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */

/* Define to prevent recursive inclusion -------------------------------------*/
#ifndef __MAIN_H
#define __MAIN_H

#ifdef __cplusplus
extern "C" {
#endif

/* Includes ------------------------------------------------------------------*/
#include "stm32f1xx_hal.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

/* USER CODE END Includes */

/* Exported types ------------------------------------------------------------*/
/* USER CODE BEGIN ET */

/* USER CODE END ET */

/* Exported constants --------------------------------------------------------*/
/* USER CODE BEGIN EC */

/* USER CODE END EC */

/* Exported macro ------------------------------------------------------------*/
/* USER CODE BEGIN EM */

/* USER CODE END EM */

/* Exported functions prototypes ---------------------------------------------*/
void Error_Handler(void);

/* USER CODE BEGIN EFP */

/* USER CODE END EFP */

/* Private defines -----------------------------------------------------------*/
/* 四组 PWM 输出：G1~G4 对应每侧从上到下四层，每个信号驱动左右两个对称阀。 */
#define VALVE_G1_Pin GPIO_PIN_0
#define VALVE_G1_GPIO_Port GPIOB
#define VALVE_G2_Pin GPIO_PIN_1
#define VALVE_G2_GPIO_Port GPIOB
#define VALVE_G3_Pin GPIO_PIN_10
#define VALVE_G3_GPIO_Port GPIOB
#define VALVE_G4_Pin GPIO_PIN_11
#define VALVE_G4_GPIO_Port GPIOB
#define VALVE_R1_Pin GPIO_PIN_0
#define VALVE_R1_GPIO_Port GPIOA
#define VALVE_R2_Pin GPIO_PIN_1
#define VALVE_R2_GPIO_Port GPIOA
#define VALVE_R3_Pin GPIO_PIN_4
#define VALVE_R3_GPIO_Port GPIOA
#define VALVE_R4_Pin GPIO_PIN_5
#define VALVE_R4_GPIO_Port GPIOA
/* USART2 外接 TTL 转 RS485，用该 DE 引脚控制电流模块总线收发方向。 */
#define CURRENT_RS485_DE_Pin GPIO_PIN_12
#define CURRENT_RS485_DE_GPIO_Port GPIOB
/* USART1 外接 TTL 转 RS485，用该 DE 引脚控制上位机总线收发方向。 */
#define HOST_RS485_DE_Pin GPIO_PIN_8
#define HOST_RS485_DE_GPIO_Port GPIOA
#define HOST_ACTIVITY_Pin GPIO_PIN_13
#define HOST_ACTIVITY_GPIO_Port GPIOC

/* USER CODE BEGIN Private defines */

/* USER CODE END Private defines */

#ifdef __cplusplus
}
#endif

#endif /* __MAIN_H */
