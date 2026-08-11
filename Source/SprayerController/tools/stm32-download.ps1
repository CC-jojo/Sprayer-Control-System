param(
    [Parameter(Mandatory = $true)]
    [string]$Programmer,

    [Parameter(Mandatory = $true)]
    [string]$Firmware
)

$ErrorActionPreference = 'Stop'

function Format-ProgrammerOutput {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromPipeline = $true)]
        [AllowEmptyString()]
        [string]$Line
    )

    begin {
        $progressKind = $null
    }

    process {
        if ($Line -match '^Download in Progress:') {
            $progressKind = 'Write'
            return
        }

        if ($Line -match '^Read progress:') {
            $progressKind = 'Read'
            return
        }

        if ($null -ne $progressKind) {
            if ($Line -match '100%') {
                if ($progressKind -eq 'Write') {
                    Write-Output 'Write progress: [####################] 100%'
                }
                else {
                    Write-Output 'Read progress:  [####################] 100%'
                }
                $progressKind = $null
            }
            return
        }

        Write-Output $Line
    }

    end {
        if ($null -ne $progressKind) {
            if ($progressKind -eq 'Write') {
                Write-Output 'Write progress: [####################] 100%'
            }
            else {
                Write-Output 'Read progress:  [####################] 100%'
            }
        }
    }
}

function Invoke-ProgrammerCapture {
    param(
        [string]$Executable,
        [string[]]$Arguments
    )

    $captured = @(& $Executable @Arguments 2>&1 | ForEach-Object { [string]$_ })
    [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $captured
    }
}

function Wait-ForStm32Target {
    param([string]$Executable)

    $profiles = @(
        'port=SWD',
        'port=SWD freq=1000',
        'port=SWD freq=100'
    )
    $lastResult = $null

    for ($index = 0; $index -lt $profiles.Count; ++$index) {
        Write-Host "SWD probe attempt $($index + 1)/$($profiles.Count): $($profiles[$index])"
        $lastResult = Invoke-ProgrammerCapture -Executable $Executable -Arguments @('-c', $profiles[$index])
        if ($lastResult.ExitCode -eq 0) {
            if ($index -gt 0) {
                Write-Host "SWD connection recovered on attempt $($index + 1)."
            }
            else {
                Write-Host 'SWD target detected.'
            }
            return $true
        }
        Start-Sleep -Milliseconds 500
    }

    if ($null -ne $lastResult) {
        $lastResult.Output | ForEach-Object { Write-Host $_ }
    }
    Write-Host 'Unable to detect STM32 after automatic retries. Check SWDIO, SWCLK, GND and target power.'
    return $false
}

if ($MyInvocation.InvocationName -eq '.') {
    return
}

if (-not (Test-Path -LiteralPath $Programmer -PathType Leaf)) {
    Write-Error "STM32 Programmer was not found: $Programmer"
    exit 2
}

if (-not (Test-Path -LiteralPath $Firmware -PathType Leaf)) {
    Write-Error "Firmware was not found: $Firmware"
    exit 3
}

if (-not (Wait-ForStm32Target -Executable $Programmer)) {
    exit 4
}

& $Programmer -c 'port=SWD' -w $Firmware -v -rst 2>&1 |
    ForEach-Object { [string]$_ } |
    Format-ProgrammerOutput

$programmerExitCode = $LASTEXITCODE
exit $programmerExitCode
