[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$DesktopSource,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FirmwareSource,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$ProjectRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Initialize-NativePathResolver {
    if ($null -ne ('SprayerTouchHMI.NativePathResolver' -as [type])) {
        return
    }

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Text;

namespace SprayerTouchHMI
{
    public static class NativePathResolver
    {
        private const uint FileFlagBackupSemantics = 0x02000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            StringBuilder path,
            uint pathLength,
            uint flags);

        public static string GetFinalDirectoryPath(string path)
        {
            using (SafeFileHandle handle = CreateFile(
                path,
                0,
                0x00000007,
                IntPtr.Zero,
                3,
                FileFlagBackupSemantics,
                IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot open directory handle.");
                }

                StringBuilder result = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(handle, result, (uint)result.Capacity, 0);
                if (length == 0 || length >= result.Capacity)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Cannot resolve final directory path.");
                }

                return result.ToString();
            }
        }
    }
}
'@
}

function ConvertTo-NormalizedPath {
    param([Parameter(Mandatory)][string]$Path)

    $normalized = $Path -replace '/', '\'
    if ($normalized.StartsWith('\\?\UNC\', [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = '\\' + $normalized.Substring(8)
    }
    elseif ($normalized.StartsWith('\\?\', [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring(4)
    }

    if ($normalized.Length -gt 3) {
        $normalized = $normalized.TrimEnd([char]'\')
    }
    return $normalized
}

function Get-CanonicalExistingDirectoryPath {
    param([Parameter(Mandatory)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force
    if (-not $item.PSIsContainer) {
        throw "Expected a directory: $Path"
    }

    Initialize-NativePathResolver
    return ConvertTo-NormalizedPath -Path ([SprayerTouchHMI.NativePathResolver]::GetFinalDirectoryPath($item.FullName))
}

function Get-CanonicalProspectiveDirectoryPath {
    param([Parameter(Mandatory)][string]$Path)

    $probe = [System.IO.Path]::GetFullPath($Path)
    $missingSegments = [System.Collections.Generic.Stack[string]]::new()
    while (-not (Test-Path -LiteralPath $probe)) {
        $leaf = Split-Path -Leaf $probe
        $parent = Split-Path -Parent $probe
        if ([string]::IsNullOrEmpty($leaf) -or [string]::IsNullOrEmpty($parent) -or
            [string]::Equals($parent, $probe, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Cannot resolve an existing ancestor for destination path: $Path"
        }
        $missingSegments.Push($leaf)
        $probe = $parent
    }

    $canonical = Get-CanonicalExistingDirectoryPath -Path $probe
    while ($missingSegments.Count -gt 0) {
        $canonical = Join-Path $canonical $missingSegments.Pop()
    }
    return ConvertTo-NormalizedPath -Path $canonical
}

function Test-PathContains {
    param(
        [Parameter(Mandatory)][string]$Parent,
        [Parameter(Mandatory)][string]$Child
    )

    if ([string]::Equals($Parent, $Child, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $prefix = $Parent
    if (-not $prefix.EndsWith('\', [System.StringComparison]::Ordinal)) {
        $prefix += '\'
    }
    return $Child.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Assert-DisjointPaths {
    param(
        [Parameter(Mandatory)][string]$FirstLabel,
        [Parameter(Mandatory)][string]$FirstPath,
        [Parameter(Mandatory)][string]$SecondLabel,
        [Parameter(Mandatory)][string]$SecondPath
    )

    if ((Test-PathContains -Parent $FirstPath -Child $SecondPath) -or
        (Test-PathContains -Parent $SecondPath -Child $FirstPath)) {
        throw "Unsafe path overlap between $FirstLabel ($FirstPath) and $SecondLabel ($SecondPath)."
    }
}

function Assert-StrictlyContainedPath {
    param(
        [Parameter(Mandatory)][string]$ParentLabel,
        [Parameter(Mandatory)][string]$ParentPath,
        [Parameter(Mandatory)][string]$ChildLabel,
        [Parameter(Mandatory)][string]$ChildPath
    )

    if ([string]::Equals($ParentPath, $ChildPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-PathContains -Parent $ParentPath -Child $ChildPath)) {
        throw "Unsafe destination path: $ChildLabel ($ChildPath) must remain strictly inside $ParentLabel ($ParentPath)."
    }
}

function Test-OrdinalSequenceEqual {
    param(
        [Parameter(Mandatory)][string[]]$Expected,
        [Parameter(Mandatory)][string[]]$Actual
    )

    if ($Expected.Count -ne $Actual.Count) {
        return $false
    }

    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if (-not [string]::Equals($Expected[$index], $Actual[$index], [System.StringComparison]::Ordinal)) {
            return $false
        }
    }
    return $true
}

function Test-ExcludedDirectoryName {
    param([Parameter(Mandatory)][string]$Name)

    return $Name -in @('bin', 'obj', 'build', '.git', '.vs') -or
        $Name -like 'cmake-build-*'
}

function Get-IncludedFiles {
    param([Parameter(Mandatory)][string]$Root)

    $files = [System.Collections.Generic.List[object]]::new()
    $directories = [System.Collections.Generic.Stack[string]]::new()
    $directories.Push($Root)

    while ($directories.Count -gt 0) {
        $directory = $directories.Pop()
        foreach ($item in Get-ChildItem -LiteralPath $directory -Force) {
            if ($item.PSIsContainer) {
                if (-not (Test-ExcludedDirectoryName -Name $item.Name)) {
                    $directories.Push($item.FullName)
                }
                continue
            }

            $relativePath = $item.FullName.Substring($Root.Length).TrimStart([char]'\', [char]' /'.Trim()) -replace '\\', '/'
            $files.Add([pscustomobject]@{
                    FullName = $item.FullName
                    RelativePath = $relativePath
                })
        }
    }

    $orderedFiles = $files.ToArray()
    [System.Array]::Sort(
        $orderedFiles,
        [System.Comparison[object]]{
            param($left, $right)
            return [string]::Compare(
                [string]$left.RelativePath,
                [string]$right.RelativePath,
                [System.StringComparison]::Ordinal)
        })
    return $orderedFiles
}

function Get-ManifestLines {
    param([Parameter(Mandatory)][object[]]$Files)

    return @($Files | ForEach-Object {
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
        "$hash  $($_.RelativePath)"
    })
}

function Get-AggregateManifestHash {
    param([Parameter(Mandatory)][string[]]$Lines)

    $content = [string]::Join("`n", $Lines) + "`n"
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($content)
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $hasher.Dispose()
    }
}

function Write-Manifest {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$Lines
    )

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $content = [string]::Join("`n", $Lines) + "`n"
    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

function Assert-DestinationIsEmpty {
    param([Parameter(Mandatory)][string]$Path)

    if (Test-Path -LiteralPath $Path) {
        if ($null -ne (Get-ChildItem -LiteralPath $Path -Force | Select-Object -First 1)) {
            throw "Refusing to copy into non-empty destination source tree: $Path"
        }
    }
}

function Initialize-EmptyDestination {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Copy-IncludedFiles {
    param(
        [Parameter(Mandatory)][object[]]$Files,
        [Parameter(Mandatory)][string]$DestinationRoot
    )

    foreach ($file in $Files) {
        $destination = Join-Path $DestinationRoot ($file.RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
}

$desktopRoot = Get-CanonicalExistingDirectoryPath -Path $DesktopSource
$firmwareRoot = Get-CanonicalExistingDirectoryPath -Path $FirmwareSource
$projectRoot = Get-CanonicalExistingDirectoryPath -Path $ProjectRoot
$desktopDestination = Join-Path $projectRoot 'Source/SprayerDesktop'
$firmwareDestination = Join-Path $projectRoot 'Source/SprayerController'
$desktopDestinationCanonical = Get-CanonicalProspectiveDirectoryPath -Path $desktopDestination
$firmwareDestinationCanonical = Get-CanonicalProspectiveDirectoryPath -Path $firmwareDestination

Assert-StrictlyContainedPath -ParentLabel 'ProjectRoot' -ParentPath $projectRoot -ChildLabel 'DesktopDestination' -ChildPath $desktopDestinationCanonical
Assert-StrictlyContainedPath -ParentLabel 'ProjectRoot' -ParentPath $projectRoot -ChildLabel 'FirmwareDestination' -ChildPath $firmwareDestinationCanonical
Assert-DisjointPaths -FirstLabel 'DesktopSource' -FirstPath $desktopRoot -SecondLabel 'FirmwareSource' -SecondPath $firmwareRoot
Assert-DisjointPaths -FirstLabel 'ProjectRoot' -FirstPath $projectRoot -SecondLabel 'DesktopSource' -SecondPath $desktopRoot
Assert-DisjointPaths -FirstLabel 'ProjectRoot' -FirstPath $projectRoot -SecondLabel 'FirmwareSource' -SecondPath $firmwareRoot
Assert-DisjointPaths -FirstLabel 'DesktopSource' -FirstPath $desktopRoot -SecondLabel 'DesktopDestination' -SecondPath $desktopDestinationCanonical
Assert-DisjointPaths -FirstLabel 'DesktopSource' -FirstPath $desktopRoot -SecondLabel 'FirmwareDestination' -SecondPath $firmwareDestinationCanonical
Assert-DisjointPaths -FirstLabel 'FirmwareSource' -FirstPath $firmwareRoot -SecondLabel 'DesktopDestination' -SecondPath $desktopDestinationCanonical
Assert-DisjointPaths -FirstLabel 'FirmwareSource' -FirstPath $firmwareRoot -SecondLabel 'FirmwareDestination' -SecondPath $firmwareDestinationCanonical
Assert-DisjointPaths -FirstLabel 'DesktopDestination' -FirstPath $desktopDestinationCanonical -SecondLabel 'FirmwareDestination' -SecondPath $firmwareDestinationCanonical

Assert-DestinationIsEmpty -Path $desktopDestination
Assert-DestinationIsEmpty -Path $firmwareDestination
Initialize-EmptyDestination -Path $desktopDestination
Initialize-EmptyDestination -Path $firmwareDestination

$desktopFiles = @(Get-IncludedFiles -Root $desktopRoot)
$firmwareFiles = @(Get-IncludedFiles -Root $firmwareRoot)
if ($desktopFiles.Count -eq 0 -or $firmwareFiles.Count -eq 0) {
    throw 'Each source baseline must contain at least one included file.'
}

$desktopBefore = @(Get-ManifestLines -Files $desktopFiles)
$firmwareBefore = @(Get-ManifestLines -Files $firmwareFiles)

Copy-IncludedFiles -Files $desktopFiles -DestinationRoot $desktopDestination
Copy-IncludedFiles -Files $firmwareFiles -DestinationRoot $firmwareDestination

$desktopCopied = @(Get-ManifestLines -Files @(Get-IncludedFiles -Root $desktopDestination))
$firmwareCopied = @(Get-ManifestLines -Files @(Get-IncludedFiles -Root $firmwareDestination))
$desktopAfter = @(Get-ManifestLines -Files @(Get-IncludedFiles -Root $desktopRoot))
$firmwareAfter = @(Get-ManifestLines -Files @(Get-IncludedFiles -Root $firmwareRoot))

if (-not (Test-OrdinalSequenceEqual -Expected $desktopBefore -Actual $desktopCopied)) {
    throw 'Desktop copied manifest does not match its baseline manifest.'
}
if (-not (Test-OrdinalSequenceEqual -Expected $firmwareBefore -Actual $firmwareCopied)) {
    throw 'Firmware copied manifest does not match its baseline manifest.'
}
if (-not (Test-OrdinalSequenceEqual -Expected $desktopBefore -Actual $desktopAfter)) {
    throw 'Desktop immutable baseline changed during copy.'
}
if (-not (Test-OrdinalSequenceEqual -Expected $firmwareBefore -Actual $firmwareAfter)) {
    throw 'Firmware immutable baseline changed during copy.'
}

Write-Manifest -Path (Join-Path $projectRoot 'Docs/BASELINE_MANIFEST_DESKTOP.sha256') -Lines $desktopBefore
Write-Manifest -Path (Join-Path $projectRoot 'Docs/BASELINE_MANIFEST_FIRMWARE.sha256') -Lines $firmwareBefore

[pscustomobject]@{
    DesktopFileCount = $desktopBefore.Count
    FirmwareFileCount = $firmwareBefore.Count
    DesktopManifestHash = Get-AggregateManifestHash -Lines $desktopBefore
    FirmwareManifestHash = Get-AggregateManifestHash -Lines $firmwareBefore
    DesktopCopyManifestHash = Get-AggregateManifestHash -Lines $desktopCopied
    FirmwareCopyManifestHash = Get-AggregateManifestHash -Lines $firmwareCopied
    DesktopBaselineUnchanged = $true
    FirmwareBaselineUnchanged = $true
    ExcludedDirectories = 'bin, obj, build, cmake-build-*, .git, .vs'
}
