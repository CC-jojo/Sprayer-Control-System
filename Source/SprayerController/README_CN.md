# STM32 下位机工程中文说明

芯片为 STM32F103C8T6，HAL 库工程，使用 STM32CubeMX 配置外设并用 CLion/CMake 开发。代码文件保留英文名称，用户可修改代码已补充中文注释。

## 最常修改的文件

- `App/pwm_engine.c`：四组 10 Hz 错相 PWM 的核心代码。
  - `PWM_DEFAULT_DUTY`：上电默认占空比，当前为 10%。
  - `phase[4]`：四组相位，当前为 0、25、50、75 个 1 ms 节拍。
  - `PwmEngine_Init()`：初始化四组 PWM。
  - `PwmEngine_SetPending()`：设置下一周期生效的四组占空比。
  - `PwmEngine_Tick1ms()`：每 1 ms 计算一次四组输出状态。
- `App/host_registers.c/.h`：上位机可修改参数、默认值和合法范围。
  - `ControllerConfig_SetDefaults()`：默认联动模式、10% 占空比、10%~100% 上下限。
  - `ControllerConfig_IsValid()`：校验占空比和0~20000微安目标电流。
  - `HostRegisters_WriteBatch()`：处理上位机写参数。
- `App/current_module.c/.h`：一路 Modbus 电流模块和回流比例阀控制。
  - `CURRENT_REGISTER`：模块输出寄存器地址，当前为 `0x000A`。
  - `CurrentModule_SetTargetMicroamps()`：直接设置0~20000微安目标电流。
  - `CurrentModule_BuildRequest()`：生成 0x06 写入和 0x03 回读命令。
- `Platform/app_hal.c/.h`：HAL、双 RS485、GPIO、Flash 和看门狗之间的连接层。
  - `HOST_SLAVE_ADDRESS`：STM32 对上位机的 Modbus 地址。
  - `CURRENT_MODULE_ADDRESS`：电流模块地址。
  - `WriteValveOutputs()`：把四组 PWM 位掩码写到四个输出引脚。
  - `SaveConfig()` / `LoadConfig()`：保存和读取 Flash 配置。
- `Core/Inc/main.h`：四组阀输出和两个 RS485 DE 引脚定义。
- `PC13`：上位机指令成功指示；上电为低，每成功处理一条合法指令翻转一次。
- `Core/Src/usart.c`：USART1 上位机 115200，USART2 电流模块 9600。
- `Core/Src/tim.c`：TIM3 的 1 ms PWM 基准中断。
- `Core/Src/main.c`：系统初始化与主循环入口，每个主要步骤已有中文注释。

## 四组 PWM 与八个阀

`VALVE_G1` 到 `VALVE_G4` 是四个控制信号。每个信号经过合适的 12 V 功率驱动电路后，同时驱动左右两侧同一高度的两个电磁阀。STM32 引脚不能直接连接 12 V 电磁阀，必须使用 MOSFET/驱动模块、续流保护和共地设计。

## 修改注意事项

1. 优先修改 `App` 目录的业务参数，不要直接修改 HAL 和 CMSIS 官方库。
2. 用 CubeMX 重新生成代码时，保留 `USER CODE BEGIN/END` 区域。
3. 占空比下限必须大于 0%；停止 PWM 输出使用上位机总开关，总开关每次上电默认关闭。
4. 修改电流模块寄存器、波特率或数据范围前，先对照实际模块手册。
5. 修改后至少编译 Debug、Release，并运行 `SprayerFirmwareTests`。

## CLion 一键下载的自动恢复

`tools/stm32-download.ps1` 会在烧录前自动探测 STM32。若首次 SWD 建链失败，脚本会依次使用 4000、1000、100 kHz 重试，探测成功后才执行下载、校验和复位；三次均失败时才退出并提示检查 SWDIO、SWCLK、GND 和目标供电。对应回归测试位于 `tools/tests`。
