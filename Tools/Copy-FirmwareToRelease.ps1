$ErrorActionPreference = 'Stop'
$base = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12'
$releaseDir = Join-Path $base (-join ([char[]](0x8FD0,0x884C,0x7248,0x672C)))
$srcDir = Join-Path $base 'Source\SprayerController\build\Release'

Copy-Item -LiteralPath (Join-Path $srcDir 'SprayerController.bin') -Destination (Join-Path $releaseDir 'SprayerController.bin') -Force
Copy-Item -LiteralPath (Join-Path $srcDir 'SprayerController.hex') -Destination (Join-Path $releaseDir 'SprayerController.hex') -Force

Write-Host 'Release folder contents:'
Get-ChildItem -LiteralPath $releaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 120
