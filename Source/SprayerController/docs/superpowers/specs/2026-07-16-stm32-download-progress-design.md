# STM32 下载进度显示设计

## 目标

保留 CLion 顶部 `STM32 Download` 一键下载流程，将 STM32CubeProgrammer 在 CLion 中显示为“北/圹”的乱码进度字符替换为规则 ASCII 进度条：

```text
Write progress: [####################] 100%
Read progress:  [####################] 100%
```

## 方案

增加一个仅负责显示清理的 PowerShell 包装脚本。脚本调用现有 `STM32_Programmer_CLI.exe`，保留设备信息、下载结果、校验结果和复位结果；识别写入及读取进度段，隐藏乱码字符行，并在完成时输出固定宽度 ASCII 进度条。

CLion 的 `STM32 Download` 运行配置改为调用该包装脚本。固件编译目标、ELF 文件、烧录参数和控制程序源码保持不变。

## 错误处理

- Programmer 启动失败时显示明确错误并返回非零退出码。
- 下载或校验失败时保留原始错误输出，并返回 Programmer 的原始退出码。
- 只替换进度字符，不隐藏成功、失败、芯片连接及复位信息。

## 验证

1. 使用模拟输出验证乱码行被替换为 ASCII 进度条。
2. 在 CLion 中点击 `STM32 Download` 实际烧录。
3. 确认输出不再出现“北/圹”，包含规则进度条、`Download verified successfully`、`MCU Reset`，且进程退出代码为 0。
