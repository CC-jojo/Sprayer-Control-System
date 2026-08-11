$ErrorActionPreference = 'Stop'

$wrapper = Join-Path $PSScriptRoot '..\stm32-download.ps1'
. $wrapper -Programmer 'unused.exe' -Firmware 'unused.elf'

$badA = [char]0x5317
$badB = [char]0x5729
$sample = @(
    'Download in Progress:',
    ("$badA" * 10),
    (("$badB" * 8) + ' 100%'),
    'File download complete',
    'Read progress:',
    ("$badA" * 10),
    (("$badB" * 8) + ' 100%'),
    'Download verified successfully'
)

$actual = @($sample | Format-ProgrammerOutput)
$joined = $actual -join "`n"

if ($joined -match "$badA|$badB") {
    throw 'Garbled progress characters were not removed.'
}
if ($joined -notmatch [regex]::Escape('Write progress: [####################] 100%')) {
    throw 'ASCII write progress bar is missing.'
}
if ($joined -notmatch [regex]::Escape('Read progress:  [####################] 100%')) {
    throw 'ASCII read progress bar is missing.'
}
if ($joined -notmatch 'Download verified successfully') {
    throw 'Programmer success output was not preserved.'
}

Write-Output 'PASS: STM32 download output uses ASCII progress bars'
