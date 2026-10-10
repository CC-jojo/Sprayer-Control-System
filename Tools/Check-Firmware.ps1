$ErrorActionPreference = 'SilentlyContinue'
$base = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12'
$releaseDir = Join-Path $base (-join ([char[]](0x8FD0,0x884C,0x7248,0x672C)))
$srcBin = Join-Path $base 'Source\SprayerController\build\Release\SprayerController.bin'

Write-Host '== Source bin =='
Get-Item -LiteralPath $srcBin | Select-Object FullName, Length, LastWriteTime | Format-List
Write-Host '== Release folder bin/hex =='
Get-ChildItem -LiteralPath $releaseDir -Filter 'SprayerController.*' | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 120
