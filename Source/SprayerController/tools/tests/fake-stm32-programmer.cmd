@echo off
setlocal enabledelayedexpansion
set /a COUNT=0
if exist "%FAKE_STM32_COUNT_FILE%" set /p COUNT=<"%FAKE_STM32_COUNT_FILE%"
set /a COUNT+=1
>"%FAKE_STM32_COUNT_FILE%" echo !COUNT!

if !COUNT! LSS 3 (
  echo Error: No STM32 target found!
  exit /b 1
)

if !COUNT! EQU 3 (
  echo Device ID   : 0x410
  exit /b 0
)

echo Download in Progress:
echo #################### 100%%
echo File download complete
echo Read progress:
echo #################### 100%%
echo Download verified successfully
exit /b 0
