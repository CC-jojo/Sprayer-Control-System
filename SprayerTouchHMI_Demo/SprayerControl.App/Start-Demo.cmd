@echo off
rem 演示入口：启动窗口化演示控制器，不需要真实串口设备。
rem 安全提示：演示数据不保存、不写串口；本文件会随 publish 复制到输出目录，可直接从发布目录运行。
start "" "%~dp0SprayerControl.App.exe" --demo --windowed
