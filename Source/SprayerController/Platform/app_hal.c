#include "app_hal.h"

#include "app_runtime.h"
#include "config_store.h"
#include "gpio.h"
#include "iwdg.h"
#include "main.h"
#include "tim.h"
#include "usart.h"

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

enum
{
    HOST_SLAVE_ADDRESS = 1U,             /* STM32 对上位机使用的 Modbus 地址。 */
    CURRENT_MODULE_ADDRESS = 1U,         /* 一路电流模块的 Modbus 地址。 */
    HOST_RX_SIZE = 64U,                  /* 上位机接收缓冲区字节数。 */
    HOST_TX_SIZE = 64U,                  /* 上位机发送缓冲区字节数。 */
    CURRENT_FRAME_SIZE = 16U,            /* 电流模块请求/响应最大缓存。 */
    CURRENT_RESPONSE_TIMEOUT_MS = 200U,  /* 等待电流模块响应的超时时间。 */
    CURRENT_VERIFY_INTERVAL_MS = 1000U,  /* 在线时每秒回读校验一次。 */
    FLASH_CONFIG_ADDRESS = 0x0800FC00U   /* STM32F103C8 最后一页 Flash 起始地址。 */
};

static AppRuntime runtime; /* 唯一的下位机业务运行时实例。 */
static uint8_t host_rx[HOST_RX_SIZE];
static uint8_t host_tx[HOST_TX_SIZE];
static uint8_t current_rx[CURRENT_FRAME_SIZE];
static uint8_t current_tx[CURRENT_FRAME_SIZE];
static bool host_tx_busy;                /* USART1 是否正在发送上位机响应。 */
static bool current_tx_busy;             /* USART2 是否正在发送模块请求。 */
static bool current_waiting_response;    /* 是否已经转入接收并等待模块响应。 */
static uint32_t current_response_deadline;
static uint32_t last_current_verify;
static uint32_t last_poll_tick;

/* 使用空闲线中断接收一整帧上位机 Modbus RTU 数据。 */
static void StartHostReceive(void)
{
    (void)HAL_UARTEx_ReceiveToIdle_IT(&huart1, host_rx, sizeof(host_rx));
}

/* 偶数位驱动左侧喷嘴，奇数位驱动右侧喷嘴；位序为 L1、R1 至 L4、R4。 */
static void WriteValveOutputs(uint8_t mask)
{
    HAL_GPIO_WritePin(VALVE_G1_GPIO_Port, VALVE_G1_Pin,
                      ((mask & 0x01U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_G2_GPIO_Port, VALVE_G2_Pin,
                      ((mask & 0x04U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_G3_GPIO_Port, VALVE_G3_Pin,
                      ((mask & 0x10U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_G4_GPIO_Port, VALVE_G4_Pin,
                      ((mask & 0x40U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_R1_GPIO_Port, VALVE_R1_Pin,
                      ((mask & 0x02U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_R2_GPIO_Port, VALVE_R2_Pin,
                      ((mask & 0x08U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_R3_GPIO_Port, VALVE_R3_Pin,
                      ((mask & 0x20U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
    HAL_GPIO_WritePin(VALVE_R4_GPIO_Port, VALVE_R4_Pin,
                      ((mask & 0x80U) != 0U) ? GPIO_PIN_SET : GPIO_PIN_RESET);
}

/* 从保留 Flash 页读取配置，只有记录有效且启用持久化才采用。 */
static bool LoadConfig(ControllerConfig *config)
{
    uint8_t record[CONFIG_STORE_RECORD_SIZE];
    for (size_t index = 0U; index < sizeof(record); ++index) {
        record[index] = *(const volatile uint8_t *)(FLASH_CONFIG_ADDRESS + index);
    }
    return ConfigStore_Decode(record, sizeof(record), config) &&
           config->persistence_enabled;
}

/* 擦除最后一页 Flash，并以半字方式写入带 CRC 的配置记录。 */
static bool SaveConfig(const ControllerConfig *config)
{
    uint8_t record[CONFIG_STORE_RECORD_SIZE];
    FLASH_EraseInitTypeDef erase = {0};
    uint32_t page_error = 0U;
    bool success = ConfigStore_Encode(config, record, sizeof(record));

    if (!success || (HAL_FLASH_Unlock() != HAL_OK)) {
        return false;
    }

    /* STM32F103C8 每页为 1 KB，本项目保留最后一页存放配置。 */
    erase.TypeErase = FLASH_TYPEERASE_PAGES;
    erase.PageAddress = FLASH_CONFIG_ADDRESS;
    erase.NbPages = 1U;
    success = HAL_FLASHEx_Erase(&erase, &page_error) == HAL_OK;

    /* F1 系列 Flash 使用 16 位半字编程。 */
    for (size_t index = 0U; success && (index < sizeof(record)); index += 2U) {
        const uint16_t halfword =
            (uint16_t)record[index] | ((uint16_t)record[index + 1U] << 8U);
        success = HAL_FLASH_Program(
            FLASH_TYPEPROGRAM_HALFWORD,
            FLASH_CONFIG_ADDRESS + index,
            halfword) == HAL_OK;
    }
    (void)HAL_FLASH_Lock();
    return success;
}

/* 初始化业务模块、载入可用配置、设置两个 485 为接收态并启动 TIM3。 */
void AppHal_Init(void)
{
    ControllerConfig saved_config;
    AppRuntime_Init(&runtime, HOST_SLAVE_ADDRESS, CURRENT_MODULE_ADDRESS);
    if (LoadConfig(&saved_config)) {
        AppRuntime_LoadConfig(&runtime, &saved_config);
    }

    host_tx_busy = false;
    current_tx_busy = false;
    current_waiting_response = false;
    last_current_verify = HAL_GetTick();
    last_poll_tick = last_current_verify;
    /* DE 低电平表示 RS485 收发器处于接收状态。 */
    HAL_GPIO_WritePin(HOST_RS485_DE_GPIO_Port, HOST_RS485_DE_Pin, GPIO_PIN_RESET);
    HAL_GPIO_WritePin(CURRENT_RS485_DE_GPIO_Port, CURRENT_RS485_DE_Pin, GPIO_PIN_RESET);
    StartHostReceive();
    if (HAL_TIM_Base_Start_IT(&htim3) != HAL_OK) {
        Error_Handler();
    }
}

/* 非阻塞主循环任务：管理电流模块通信、配置保存和看门狗。 */
void AppHal_Poll(void)
{
    const uint32_t now = HAL_GetTick();
    const uint32_t elapsed = now - last_poll_tick;
    last_poll_tick = now;
    if (elapsed != 0U) {
        CurrentModule_Tick(
            &runtime.current_module,
            (elapsed > UINT16_MAX) ? UINT16_MAX : (uint16_t)elapsed);
    }

    /* 使用有符号差值判断计时到期，可正确跨越 32 位毫秒计数回绕。 */
    if (current_waiting_response && ((int32_t)(now - current_response_deadline) >= 0)) {
        (void)HAL_UART_AbortReceive_IT(&huart2);
        current_waiting_response = false;
        CurrentModule_OnTimeout(&runtime.current_module);
        AppRuntime_RefreshStatus(&runtime);
    }

    /* 模块在线时周期性回读，监视实际输出是否仍与目标一致。 */
    if ((runtime.current_module.state == CURRENT_ONLINE) &&
        ((now - last_current_verify) >= CURRENT_VERIFY_INTERVAL_MS)) {
        CurrentModule_RequestVerify(&runtime.current_module);
        last_current_verify = now;
    }

    /* USART2 空闲时才允许状态机构造并发送下一条请求。 */
    if (!current_tx_busy && !current_waiting_response) {
        const size_t length = CurrentModule_BuildRequest(
            &runtime.current_module, current_tx, sizeof(current_tx));
        if (length != 0U) {
            current_tx_busy = true;
            /* 发送前将电流模块 RS485 的 DE 拉高，切换到发送态。 */
            HAL_GPIO_WritePin(
                CURRENT_RS485_DE_GPIO_Port, CURRENT_RS485_DE_Pin, GPIO_PIN_SET);
            if (HAL_UART_Transmit_IT(&huart2, current_tx, (uint16_t)length) != HAL_OK) {
                current_tx_busy = false;
                HAL_GPIO_WritePin(
                    CURRENT_RS485_DE_GPIO_Port, CURRENT_RS485_DE_Pin, GPIO_PIN_RESET);
                CurrentModule_OnTimeout(&runtime.current_module);
            }
        }
    }

    /* 保存命令由寄存器层置位，Flash 操作统一在主循环执行。 */
    if (runtime.host.save_requested) {
        runtime.host.save_requested = false;
        if (!SaveConfig(&runtime.host.config)) {
            runtime.host.status.last_parameter_error = 0x1001U;
        }
    }

    AppRuntime_RefreshStatus(&runtime);
    HAL_IWDG_Refresh(&hiwdg); /* 主循环正常运行时刷新独立看门狗。 */
}

/* TIM3 每 1 ms 进入一次：计算并立即更新四组软件 PWM 输出。 */
void HAL_TIM_PeriodElapsedCallback(TIM_HandleTypeDef *htim)
{
    if (htim->Instance == TIM3) {
        WriteValveOutputs(AppRuntime_Tick500us(&runtime));
    }
}

/* 串口空闲事件表示收到一帧：USART1 属于上位机，USART2 属于电流模块。 */
void HAL_UARTEx_RxEventCallback(UART_HandleTypeDef *huart, uint16_t size)
{
    if (huart->Instance == USART1) {
        const size_t response_length = AppRuntime_ProcessHostFrame(
            &runtime, host_rx, size, host_tx, sizeof(host_tx));
        HAL_GPIO_WritePin(
            HOST_ACTIVITY_GPIO_Port,
            HOST_ACTIVITY_Pin,
            runtime.host_activity_level ? GPIO_PIN_SET : GPIO_PIN_RESET);
        /* 广播、CRC 错误或无需响应时直接重新接收下一帧。 */
        if (response_length == 0U) {
            StartHostReceive();
        } else {
            host_tx_busy = true;
            HAL_GPIO_WritePin(HOST_RS485_DE_GPIO_Port, HOST_RS485_DE_Pin, GPIO_PIN_SET);
            if (HAL_UART_Transmit_IT(&huart1, host_tx, (uint16_t)response_length) != HAL_OK) {
                host_tx_busy = false;
                HAL_GPIO_WritePin(
                    HOST_RS485_DE_GPIO_Port, HOST_RS485_DE_Pin, GPIO_PIN_RESET);
                StartHostReceive();
            }
        }
    } else if (huart->Instance == USART2) {
        current_waiting_response = false;
        if (!CurrentModule_OnResponse(&runtime.current_module, current_rx, size)) {
            CurrentModule_OnTimeout(&runtime.current_module);
        }
        AppRuntime_RefreshStatus(&runtime);
    }
}

/* 发送结束后把对应 RS485 的 DE 拉低，重新进入接收状态。 */
void HAL_UART_TxCpltCallback(UART_HandleTypeDef *huart)
{
    if (huart->Instance == USART1) {
        host_tx_busy = false;
        HAL_GPIO_WritePin(HOST_RS485_DE_GPIO_Port, HOST_RS485_DE_Pin, GPIO_PIN_RESET);
        StartHostReceive();
    } else if (huart->Instance == USART2) {
        current_tx_busy = false;
        HAL_GPIO_WritePin(
            CURRENT_RS485_DE_GPIO_Port, CURRENT_RS485_DE_Pin, GPIO_PIN_RESET);
        /* 电流模块请求发完后立即启动带空闲检测的响应接收。 */
        current_waiting_response = true;
        current_response_deadline = HAL_GetTick() + CURRENT_RESPONSE_TIMEOUT_MS;
        if (HAL_UARTEx_ReceiveToIdle_IT(&huart2, current_rx, sizeof(current_rx)) != HAL_OK) {
            current_waiting_response = false;
            CurrentModule_OnTimeout(&runtime.current_module);
        }
    }
}

/* 串口异常时恢复接收方向；USART2 异常同时计为一次模块通信超时。 */
void HAL_UART_ErrorCallback(UART_HandleTypeDef *huart)
{
    if (huart->Instance == USART1) {
        host_tx_busy = false;
        HAL_GPIO_WritePin(HOST_RS485_DE_GPIO_Port, HOST_RS485_DE_Pin, GPIO_PIN_RESET);
        StartHostReceive();
    } else if (huart->Instance == USART2) {
        current_tx_busy = false;
        current_waiting_response = false;
        HAL_GPIO_WritePin(
            CURRENT_RS485_DE_GPIO_Port, CURRENT_RS485_DE_Pin, GPIO_PIN_RESET);
        CurrentModule_OnTimeout(&runtime.current_module);
        AppRuntime_RefreshStatus(&runtime);
    }
}
