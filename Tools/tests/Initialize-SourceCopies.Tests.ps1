[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$copyScript = Join-Path $PSScriptRoot '..\Initialize-SourceCopies.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("SprayerTouchHMI-CopyTests-" + [guid]::NewGuid().ToString('N'))

function Assert-True {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-TreeSnapshot {
    param([Parameter(Mandatory)][string]$Root)

    $rootPath = (Get-Item -LiteralPath $Root -Force).FullName.TrimEnd([char]'\')
    $entries = [System.Collections.Generic.List[string]]::new()
    $entries.Add('D|.')

    foreach ($item in Get-ChildItem -LiteralPath $rootPath -Force -Recurse) {
        $relative = $item.FullName.Substring($rootPath.Length).TrimStart([char]'\') -replace '\\', '/'
        if ($item.PSIsContainer) {
            $entries.Add("D|$relative")
        }
        else {
            $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
            $entries.Add("F|$relative|$hash")
        }
    }

    $result = $entries.ToArray()
    [System.Array]::Sort($result, [System.StringComparer]::Ordinal)
    return ,$result
}

function Assert-OrdinalSequenceEqual {
    param(
        [Parameter(Mandatory)][string[]]$Expected,
        [Parameter(Mandatory)][string[]]$Actual,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Expected.Count -ne $Actual.Count) {
        throw "$Message Count differs: expected $($Expected.Count), actual $($Actual.Count)."
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if (-not [string]::Equals($Expected[$index], $Actual[$index], [System.StringComparison]::Ordinal)) {
            throw "$Message Difference at index $index."
        }
    }
}

function Invoke-RefusalWithoutMutation {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Desktop,
        [Parameter(Mandatory)][string]$Firmware,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string[]]$SnapshotRoots
    )

    $before = @($SnapshotRoots | ForEach-Object { Get-TreeSnapshot -Root $_ })
    $refused = $false
    try {
        & $copyScript -DesktopSource $Desktop -FirmwareSource $Firmware -ProjectRoot $Project | Out-Null
    }
    catch {
        $refused = $true
    }

    Assert-True -Condition $refused -Message "$Name was not refused."
    $after = @($SnapshotRoots | ForEach-Object { Get-TreeSnapshot -Root $_ })
    Assert-OrdinalSequenceEqual -Expected $before -Actual $after -Message "$Name mutated a fixture before refusal."
    Write-Output "PASS: $Name"
}

function New-BaselineTree {
    param([Parameter(Mandatory)][string]$Path)

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $Path 'baseline.txt'), 'immutable test baseline')
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    $desktop = Join-Path $testRoot 'desktop'
    $firmware = Join-Path $testRoot 'firmware'
    New-BaselineTree -Path $desktop
    New-BaselineTree -Path $firmware

    $insideProject = Join-Path $desktop 'project-inside-desktop'
    New-Item -ItemType Directory -Path $insideProject -Force | Out-Null
    Invoke-RefusalWithoutMutation -Name 'project root inside desktop baseline' -Desktop $desktop -Firmware $firmware -Project $insideProject -SnapshotRoots @($desktop, $firmware)

    Invoke-RefusalWithoutMutation -Name 'project root equal to desktop baseline' -Desktop $desktop -Firmware $firmware -Project $desktop -SnapshotRoots @($desktop, $firmware)

    $junctionProject = Join-Path $testRoot 'project-junction'
    New-Item -ItemType Junction -Path $junctionProject -Target $desktop | Out-Null
    Invoke-RefusalWithoutMutation -Name 'junction project root resolving into desktop baseline' -Desktop $desktop -Firmware $firmware -Project $junctionProject -SnapshotRoots @($desktop, $firmware)

    $overlapFirmware = Join-Path $desktop 'nested-firmware'
    New-BaselineTree -Path $overlapFirmware
    $safeProject = Join-Path $testRoot 'safe-project'
    New-Item -ItemType Directory -Path $safeProject -Force | Out-Null
    Invoke-RefusalWithoutMutation -Name 'overlapping baseline roots' -Desktop $desktop -Firmware $overlapFirmware -Project $safeProject -SnapshotRoots @($desktop, $safeProject)

    $containedProject = Join-Path $testRoot 'contained-project'
    $containedDesktop = Join-Path $containedProject 'Source\SprayerDesktop\baseline'
    $containedFirmware = Join-Path $testRoot 'contained-firmware'
    New-BaselineTree -Path $containedDesktop
    New-BaselineTree -Path $containedFirmware
    Invoke-RefusalWithoutMutation -Name 'desktop baseline contained by destination tree' -Desktop $containedDesktop -Firmware $containedFirmware -Project $containedProject -SnapshotRoots @($containedProject, $containedFirmware)

    $escapeProject = Join-Path $testRoot 'escape-project'
    $escapeTarget = Join-Path $testRoot 'escape-target'
    New-Item -ItemType Directory -Path $escapeProject -Force | Out-Null
    New-Item -ItemType Directory -Path $escapeTarget -Force | Out-Null
    New-Item -ItemType Junction -Path (Join-Path $escapeProject 'Source') -Target $escapeTarget | Out-Null
    Invoke-RefusalWithoutMutation -Name 'project Source junction escaping canonical project root' -Desktop $desktop -Firmware $firmware -Project $escapeProject -SnapshotRoots @($escapeProject, $escapeTarget, $desktop, $firmware)

    $partialProject = Join-Path $testRoot 'partial-project'
    $partialFirmwareDestination = Join-Path $partialProject 'Source\SprayerController'
    New-Item -ItemType Directory -Path $partialFirmwareDestination -Force | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $partialFirmwareDestination 'already-present.txt'), 'must not be overwritten')
    Invoke-RefusalWithoutMutation -Name 'second non-empty destination prevents first destination creation' -Desktop $desktop -Firmware $firmware -Project $partialProject -SnapshotRoots @($partialProject, $desktop, $firmware)
    Assert-True -Condition (-not (Test-Path -LiteralPath (Join-Path $partialProject 'Source\SprayerDesktop'))) -Message 'Desktop destination was created before the non-empty firmware destination was rejected.'

    $validDesktop = Join-Path $testRoot 'valid-desktop'
    $validFirmware = Join-Path $testRoot 'valid-firmware'
    New-BaselineTree -Path $validDesktop
    New-BaselineTree -Path $validFirmware
    [System.IO.File]::WriteAllText((Join-Path $validDesktop 'Z.txt'), 'z')
    [System.IO.File]::WriteAllText((Join-Path $validDesktop 'a.txt'), 'a')
    [System.IO.File]::WriteAllText((Join-Path $validDesktop 'Ä.txt'), 'umlaut')
    $projectA = Join-Path $testRoot 'project-a'
    $projectB = Join-Path $testRoot 'project-b'
    New-Item -ItemType Directory -Path $projectA -Force | Out-Null
    New-Item -ItemType Directory -Path $projectB -Force | Out-Null

    $priorCulture = [System.Globalization.CultureInfo]::CurrentCulture
    $priorUiCulture = [System.Globalization.CultureInfo]::CurrentUICulture
    try {
        [System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('sv-SE')
        [System.Threading.Thread]::CurrentThread.CurrentUICulture = [System.Globalization.CultureInfo]::GetCultureInfo('sv-SE')
        & $copyScript -DesktopSource $validDesktop -FirmwareSource $validFirmware -ProjectRoot $projectA | Out-Null
    }
    finally {
        [System.Threading.Thread]::CurrentThread.CurrentCulture = $priorCulture
        [System.Threading.Thread]::CurrentThread.CurrentUICulture = $priorUiCulture
    }

    & $copyScript -DesktopSource $validDesktop -FirmwareSource $validFirmware -ProjectRoot $projectB | Out-Null
    $manifestA = [System.IO.File]::ReadAllBytes((Join-Path $projectA 'Docs\BASELINE_MANIFEST_DESKTOP.sha256'))
    $manifestB = [System.IO.File]::ReadAllBytes((Join-Path $projectB 'Docs\BASELINE_MANIFEST_DESKTOP.sha256'))
    Assert-True -Condition ($manifestA.Length -eq $manifestB.Length) -Message 'Deterministic manifest lengths differ.'
    for ($index = 0; $index -lt $manifestA.Length; $index++) {
        Assert-True -Condition ($manifestA[$index] -eq $manifestB[$index]) -Message "Deterministic manifests differ at byte $index."
    }

    $manifestLines = @(Get-Content -LiteralPath (Join-Path $projectA 'Docs\BASELINE_MANIFEST_DESKTOP.sha256'))
    $relativePaths = @($manifestLines | ForEach-Object { $_.Substring($_.IndexOf('  ', [System.StringComparison]::Ordinal) + 2) })
    $ordinalPaths = [string[]]$relativePaths.Clone()
    [System.Array]::Sort($ordinalPaths, [System.StringComparer]::Ordinal)
    Assert-OrdinalSequenceEqual -Expected $ordinalPaths -Actual $relativePaths -Message 'Manifest paths are not ordinal-sorted.'
    Write-Output 'PASS: ordinal manifest output is deterministic across cultures'
    Write-Output 'PASS: Initialize-SourceCopies safety and deterministic tests'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
