$ErrorActionPreference = 'SilentlyContinue'
$base = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12'
$releaseDir = Join-Path $base (-join ([char[]](0x8FD0,0x884C,0x7248,0x672C)))
$srcBin = Join-Path $base 'Source\SprayerController\build\Release\SprayerController.bin'
$srcHex = Join-Path $base 'Source\SprayerController\build\Release\SprayerController.hex'

Copy-Item -LiteralPath $srcBin -Destination (Join-Path $releaseDir 'SprayerController.bin') -Force
if (Test-Path -LiteralPath $srcHex) {
    Copy-Item -LiteralPath $srcHex -Destination (Join-Path $releaseDir 'SprayerController.hex') -Force
}
Write-Host 'Copied. Firmware file:'
Get-Item -LiteralPath (Join-Path $releaseDir 'SprayerController.bin') | Select-Object FullName, Length, LastWriteTime | Format-List
