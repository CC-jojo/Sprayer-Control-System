# 施药机触摸上位机配套发布包 1.0.0

## 配套关系

- 桌面端与固件均来自打包前提交 `be19ee618c9a80a9718147ff760b416972c05502`。
- 配套固件：版本 `0x0202`、协议版本 `2`、显式八喷头能力位 `0x0001`、命令寄存器 `0x000C`。
- 桌面端：`Desktop/`，Windows `win-x64`、框架依赖型 .NET 8 WPF 发布；启动 `SprayerControl.App.exe` 前请安装 .NET 8 Windows Desktop Runtime。
- 固件：`Firmware/SprayerController.elf`、`.hex`、`.bin`，仅来自本次全新 ARM Release 构建。

## 校验与限制

- 从项目根目录使用 `Release/SHA256SUMS.txt` 校验发布目录中的每一个文件（校验文件本身除外）；清单内路径均相对于项目根目录。
- 已完成桌面自动测试、固件主机测试、固件工具测试、ARM 编译和六个分辨率的演示截图验证。
- 本包未执行 STM32 烧录、串口实机、触摸屏实机或喷雾输出验收；不得以软件测试结果替代现场安全验收。
