/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : STM32 主程序入口、系统时钟配置和错误处理
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * 本软件的许可条款位于软件组件根目录的 LICENSE 文件中。
  * 如果没有随附 LICENSE 文件，则本软件按“原样”提供。
  *
  ******************************************************************************
  */
/* USER CODE END Header */

/* Includes ------------------------------------------------------------------*/
#include "main.h"   // 引入芯片主头文件以及本工程的 GPIO 引脚定义。
#include "iwdg.h"   // 引入独立看门狗 IWDG 的初始化函数声明。
#include "tim.h"    // 引入定时器函数声明，本工程使用 TIM3 产生 1 ms 节拍。
#include "usart.h"  // 引入 USART1 和 USART2 的初始化函数及句柄声明。
#include "gpio.h"   // 引入 GPIO 初始化函数声明。

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include "app_hal.h"  // 引入施药机业务层与 STM32 HAL 之间的硬件适配接口。

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

/* USER CODE BEGIN PV */

/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);  // 声明系统时钟配置函数，main() 会在外设初始化前调用它。
/* USER CODE BEGIN PFP */

/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/**
  * @brief  程序入口：初始化 STM32 硬件，然后持续执行施药机控制任务。
  * @retval int 理论返回值；嵌入式主循环不会正常返回。
  */
int main(void)  // C 程序从 main 函数开始执行。
{               // main 函数体开始。

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* 复位所有 HAL 外设状态，初始化 Flash 接口，并建立 1 ms SysTick 时间基准。 */
  HAL_Init();  // 必须先初始化 HAL，后续时钟和外设函数才能正常使用。

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* 配置系统时钟：外部高速晶振经 PLL 倍频到 72 MHz，并启动 LSI。 */
  SystemClock_Config();  // 设置 CPU、AHB、APB1 和 APB2 总线时钟。

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* 按依赖顺序初始化 CubeMX 中配置的所有外设。 */
  MX_GPIO_Init();         // 初始化四组阀门输出和两路 RS485 DE 方向控制引脚。
  MX_TIM3_Init();         // 初始化 TIM3，使其为软件 PWM 提供每 1 ms 一次的中断。
  MX_USART1_UART_Init();  // 初始化 USART1：连接上位机 RS485，115200、8N1。
  MX_USART2_UART_Init();  // 初始化 USART2：连接电流模块 RS485，9600、8N1。
  MX_IWDG_Init();         // 最后启动独立看门狗，防止程序异常卡死后长期失控。

  /* USER CODE BEGIN 2 */
  AppHal_Init();  // 初始化业务运行时、读取 Flash 参数，并启动 PWM 与串口接收。

  /* USER CODE END 2 */

  /* 主循环会一直运行，STM32 正常工作期间不会退出。 */
  /* USER CODE BEGIN WHILE */
  while (1)  // 无限循环，持续处理通信、参数和电流模块状态机。
  {          // while 主循环体开始。
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
    AppHal_Poll();  // 执行非阻塞业务任务，并在函数内部喂独立看门狗。
  }                 // 本次循环结束，立即返回 while 条件继续下一次轮询。
  /* USER CODE END 3 */
}  // main 函数体结束；正常情况下程序永远不会执行到这里。

/**
  * @brief  配置 STM32F103 的振荡器、PLL、CPU 时钟和各级总线时钟。
  * @retval None 无返回值；配置失败时进入 Error_Handler()。
  */
void SystemClock_Config(void)  // CubeMX 生成的系统时钟配置函数。
{                              // SystemClock_Config 函数体开始。
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};  // 创建振荡器配置结构体并全部清零。
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};  // 创建总线时钟配置结构体并全部清零。

  /* 配置 HSE、HSI、LSI 和 PLL：HSE 作为 PLL 输入，LSI 提供给独立看门狗。 */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_LSI|RCC_OSCILLATORTYPE_HSE;  // 指定需要配置 LSI 与 HSE 振荡器。
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;                  // 打开外部高速晶振 HSE。
  RCC_OscInitStruct.HSEPredivValue = RCC_HSE_PREDIV_DIV1;  // HSE 输入不预分频，保持原始频率。
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;                  // 同时保持内部高速振荡器 HSI 开启。
  RCC_OscInitStruct.LSIState = RCC_LSI_ON;                  // 打开内部低速振荡器 LSI，为 IWDG 提供独立时钟。
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;              // 打开 PLL 倍频器。
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;      // 选择外部高速晶振 HSE 作为 PLL 输入源。
  RCC_OscInitStruct.PLL.PLLMUL = RCC_PLL_MUL9;              // 将 8 MHz HSE 倍频 9 倍，得到 72 MHz。
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)      // 把振荡器配置写入硬件，并检查是否成功。
  {                                                         // 振荡器配置失败分支开始。
    Error_Handler();  // 配置失败时关闭中断并停在安全死循环中，防止带错误时钟继续运行。
  }                 // 振荡器配置失败分支结束。

  /* 配置 CPU、AHB、APB1、APB2 总线的时钟来源和分频系数。 */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK  // 本行选择 HCLK 与 SYSCLK 作为待配置对象，表达式在下一行继续。
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;  // 同时选择 APB1 的 PCLK1 与 APB2 的 PCLK2。
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;  // 使用 PLL 输出的 72 MHz 作为系统时钟 SYSCLK。
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;         // AHB 不分频，因此 HCLK 和 CPU 时钟均为 72 MHz。
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;          // APB1 分频为 2，满足其最高 36 MHz 限制。
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;          // APB2 不分频，PCLK2 保持 72 MHz。

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK)  // 应用总线时钟，并为 72 MHz 设置 Flash 两个等待周期。
  {                                                                         // 总线时钟配置失败分支开始。
    Error_Handler();  // 总线或 Flash 延迟配置失败时进入统一错误处理。
  }                 // 总线时钟配置失败分支结束。
}  // SystemClock_Config 函数体结束。

/* USER CODE BEGIN 4 */

/* USER CODE END 4 */

/**
  * @brief  HAL 初始化或时钟配置出错时执行的统一错误处理函数。
  * @retval None 此函数进入死循环，不会返回。
  */
void Error_Handler(void)  // 定义全局错误处理函数，其他 CubeMX 代码也可以调用它。
{                         // Error_Handler 函数体开始。
  /* USER CODE BEGIN Error_Handler_Debug */
  /* 当前实现先关闭全部可屏蔽中断，再停在死循环中，便于调试器定位故障。 */
  __disable_irq();  // 设置 CPU 的 PRIMASK，禁止所有可屏蔽中断继续执行。
  while (1)         // 永久停留在此处，避免在硬件配置失败后继续控制阀门。
  {                 // 错误死循环体开始。
  }                 // 错误死循环体结束；条件恒真，因此不会退出。
  /* USER CODE END Error_Handler_Debug */
}  // Error_Handler 函数体结束。

#ifdef USE_FULL_ASSERT  // 仅在工程启用完整参数断言功能时编译下面的诊断函数。
/**
  * @brief  报告触发 assert_param 断言的源文件名和代码行号。
  * @param  file 指向发生断言错误的源文件名字符串。
  * @param  line 发生断言错误的源代码行号。
  * @retval None 当前默认实现不输出信息，用户可在保护区中添加日志。
  */
void assert_failed(uint8_t *file, uint32_t line)  // 接收 HAL 传入的错误文件名与行号。
{                                                 // assert_failed 函数体开始。
  /* USER CODE BEGIN 6 */
  /* 可在这里通过串口输出 file 和 line，例如使用 printf 发送诊断信息。 */
  /* USER CODE END 6 */
}  // assert_failed 函数体结束。
#endif /* USE_FULL_ASSERT */  // 结束 USE_FULL_ASSERT 条件编译区域。
