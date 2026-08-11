$ErrorActionPreference = 'Stop'

$wrapper = Join-Path $PSScriptRoot '..\stm32-download.ps1'
$programmer = Join-Path $PSScriptRoot 'fake-stm32-programmer.cmd'
$countFile = Join-Path $env:TEMP 'sprayer-stm32-download-attempt-count.txt'
$env:FAKE_STM32_COUNT_FILE = $countFile
Remove-Item -LiteralPath $countFile -Force -ErrorAction SilentlyContinue

try {
    $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $wrapper `
        -Programmer $programmer -Firmware $wrapper 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Wrapper failed with exit code $LASTEXITCODE.`n$($output -join "`n")"
    }

    $attempts = [int](Get-Content -LiteralPath $countFile -Raw)
    if ($attempts -ne 4) {
        throw "Expected 3 probes plus 1 download, actual attempts: $attempts"
    }

    $joined = $output -join "`n"
    if ($joined -notmatch 'SWD connection recovered') {
        throw 'Recovery message is missing.'
    }
    if ($joined -notmatch 'Download verified successfully') {
        throw 'Successful download output is missing.'
    }
}
finally {
    Remove-Item -LiteralPath $countFile -Force -ErrorAction SilentlyContinue
    Remove-Item Env:\FAKE_STM32_COUNT_FILE -ErrorAction SilentlyContinue
}

Write-Output 'PASS: STM32 download retries and recovers before programming'
