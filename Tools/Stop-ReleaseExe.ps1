$ErrorActionPreference = 'SilentlyContinue'
$runDir = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12\' + -join ([char[]](0x8FD0,0x884C,0x7248,0x672C))
Get-Process | Where-Object { $_.Path -like "$runDir*" } | ForEach-Object {
    Write-Host ("Stopping: " + $_.Name)
    Stop-Process -Force -Id $_.Id
}
Start-Sleep -Milliseconds 500
