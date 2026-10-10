$ErrorActionPreference = 'SilentlyContinue'
$base = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12'
Get-Process | ForEach-Object {
    $p = $_.Path
    if ($p -like "$base*") {
        Write-Host ("PID=" + $_.Id + " Name=" + $_.Name + " Path=" + $p)
        Stop-Process -Force -Id $_.Id
    }
}
Start-Sleep -Milliseconds 500
Write-Host "---done---"