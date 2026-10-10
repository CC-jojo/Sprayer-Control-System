$ErrorActionPreference = 'SilentlyContinue'
$releaseDir = Join-Path 'E:\CodexFILE\SprayerTouchHMI_2026-08-12' (-join ([char[]](0x8FD0,0x884C,0x7248,0x672C)))
Get-ChildItem -LiteralPath $releaseDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String -Width 120
