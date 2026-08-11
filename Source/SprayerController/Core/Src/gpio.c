/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file    gpio.c
  * @brief   This file provides code for the configuration
  *          of all used GPIO pins.
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

/* Includes ------------------------------------------------------------------*/
#include "gpio.h"

/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/*----------------------------------------------------------------------------*/
/* Configure GPIO                                                             */
/*----------------------------------------------------------------------------*/
/* USER CODE BEGIN 1 */

/* USER CODE END 1 */

/** Configure pins as
        * Analog
        * Input
        * Output
        * EVENT_OUT
        * EXTI
*/
void MX_GPIO_Init(void)
{

  GPIO_InitTypeDef GPIO_InitStruct = {0};

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOD_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();
  __HAL_RCC_GPIOB_CLK_ENABLE();
  __HAL_RCC_GPIOC_CLK_ENABLE();

  /* PC13 上电默认为低；每成功处理一条上位机指令后翻转一次。 */
  HAL_GPIO_WritePin(HOST_ACTIVITY_GPIO_Port, HOST_ACTIVITY_Pin, GPIO_PIN_RESET);

  /* 上电先把四组阀输出及电流模块 DE 全部置低，避免初始化过程误动作。 */
  HAL_GPIO_WritePin(GPIOB, VALVE_G1_Pin|VALVE_G2_Pin|VALVE_G3_Pin|VALVE_G4_Pin
                          |CURRENT_RS485_DE_Pin, GPIO_PIN_RESET);

  /* 上位机 RS485 默认处于接收态。 */
  HAL_GPIO_WritePin(HOST_RS485_DE_GPIO_Port, HOST_RS485_DE_Pin, GPIO_PIN_RESET);
  HAL_GPIO_WritePin(GPIOA, VALVE_R1_Pin|VALVE_R2_Pin|VALVE_R3_Pin|VALVE_R4_Pin, GPIO_PIN_RESET);

  /* 四组 PWM 和电流模块 DE 均配置为低速推挽输出。 */
  /*Configure GPIO pins : VALVE_G1_Pin VALVE_G2_Pin VALVE_G3_Pin VALVE_G4_Pin
                           CURRENT_RS485_DE_Pin */
  GPIO_InitStruct.Pin = VALVE_G1_Pin|VALVE_G2_Pin|VALVE_G3_Pin|VALVE_G4_Pin
                          |CURRENT_RS485_DE_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOB, &GPIO_InitStruct);

  GPIO_InitStruct.Pin = VALVE_R1_Pin|VALVE_R2_Pin|VALVE_R3_Pin|VALVE_R4_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /* 上位机 RS485 DE 配置为低速推挽输出。 */
  /*Configure GPIO pin : HOST_RS485_DE_Pin */
  GPIO_InitStruct.Pin = HOST_RS485_DE_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(HOST_RS485_DE_GPIO_Port, &GPIO_InitStruct);

  /* 上位机通信成功翻转指示输出。 */
  GPIO_InitStruct.Pin = HOST_ACTIVITY_Pin;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_NOPULL;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(HOST_ACTIVITY_GPIO_Port, &GPIO_InitStruct);

}

/* USER CODE BEGIN 2 */

/* USER CODE END 2 */
