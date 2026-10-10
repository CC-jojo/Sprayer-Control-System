<#
.SYNOPSIS
    Build and publish both editions (formal + DEMO) into the 运行版本 (release) folder.
    Old EXE files are removed before copying the new ones, so the folder always
    keeps a single up-to-date set.

.DESCRIPTION
    - Formal edition : assembly name = SprayerControl.App (scans real controller)
    - DEMO edition   : assembly name = SprayerTouchHMI_Demo (built-in simulation)
    The DEMO app decides its mode at startup via IsDemoAssembly(), so the
    assembly name MUST be SprayerTouchHMI_Demo for a DEMO distribution.

.PARAMETER ReadmePath
    Optional path to a 使用说明.txt that should be copied into the release folder.
#>
[CmdletBinding()]
param(
    [string]$ReadmePath = ''
)

$ErrorActionPreference = 'Stop'

# --- Chinese names are built from Unicode code points to stay safe under
#     PowerShell 5.1 which reads BOM-less .ps1 files as ANSI. ---
function New-Chinese([int[]]$codes) { -join ($codes | ForEach-Object { [char]$_ }) }

$NameRunDir = New-Chinese 0x8FD0,0x884C,0x7248,0x672C   # 运行版本
$NameFormal = New-Chinese 0x65BD,0x836F,0x673A,0x63A7,0x5236,0x7CFB,0x7EDF,0x002D,0x6B63,0x5F0F,0x7248,0x002E,0x0065,0x0078,0x0065 # 施药机控制系统-正式版.exe
$NameDemo   = New-Chinese 0x65BD,0x836F,0x673A,0x63A7,0x5236,0x7CFB,0x7EDF,0x002D,0x0044,0x0045,0x004D,0x004F,0x002D,0x6F14,0x793A,0x7248,0x002E,0x0065,0x0078,0x0065 # 施药机控制系统-DEMO-演示版.exe
$NameReadme = New-Chinese 0x4F7F,0x7528,0x8BF4,0x660E,0x002E,0x0074,0x0078,0x0074 # 使用说明.txt

$Root        = Split-Path -Parent $PSScriptRoot
$FormalProj  = Join-Path $Root 'Source\SprayerDesktop\SprayerControl.App\SprayerControl.App.csproj'
$DemoProj    = Join-Path $Root 'SprayerTouchHMI_Demo\SprayerControl.App\SprayerControl.App.csproj'
$FormalOut   = Join-Path $Root 'Source\SprayerDesktop\Publish_Release'
$DemoOut     = Join-Path $Root 'SprayerTouchHMI_Demo\Publish'
$RunDir      = Join-Path $Root $NameRunDir

function Reset-Dir([string]$path) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    New-Item -ItemType Directory -Path $path | Out-Null
}

function Invoke-Publish([string]$project, [string]$outDir, [string]$extraProperty) {
    Write-Host "==> dotnet publish $project ($extraProperty)" -ForegroundColor Cyan
    Reset-Dir $outDir
    # IncludeNativeLibrariesForSelfExtract is REQUIRED for WPF single-file apps,
    # otherwise PresentationNative_cor3.dll etc. are not found at runtime
    # (System.DllNotFoundException -> crash when the window is created).
    $args = @('publish', $project, '-c', 'Release', '-r', 'win-x64',
              '--self-contained', 'true', '-p:PublishSingleFile=true',
              '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', $outDir)
    if ($extraProperty) { $args += $extraProperty }
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE): $project" }
}

Write-Host "Root : $Root"
Write-Host "Run  : $RunDir"

# 1. Publish formal edition (assembly name stays SprayerControl.App)
Invoke-Publish $FormalProj $FormalOut ''

# 2. Publish DEMO edition; IsDemoBuild flips AssemblyName to SprayerTouchHMI_Demo
Invoke-Publish $DemoProj $DemoOut '-p:IsDemoBuild=true'

$formalExe = Join-Path $FormalOut 'SprayerControl.App.exe'
$demoExe   = Join-Path $DemoOut 'SprayerTouchHMI_Demo.exe'
if (-not (Test-Path -LiteralPath $formalExe)) { throw "Formal EXE was not produced: $formalExe" }
if (-not (Test-Path -LiteralPath $demoExe))   { throw "DEMO EXE was not produced: $demoExe" }

# 3. Stop any running instance of the old EXEs (a running EXE would lock the file)
Write-Host "==> Stopping running instances of old EXEs" -ForegroundColor Cyan
Get-ChildItem -LiteralPath $RunDir -Filter *.exe -ErrorAction SilentlyContinue | ForEach-Object {
    $exePath = $_.FullName
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exePath } |
        Stop-Process -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Milliseconds 300

# 4. Clean old EXE files in the release folder (keep non-EXE files such as readme)
Write-Host "==> Cleaning old EXE files in $RunDir" -ForegroundColor Cyan
Get-ChildItem -LiteralPath $RunDir -Filter *.exe -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

# 4. Copy new binaries
$formalDest = Join-Path $RunDir $NameFormal
$demoDest   = Join-Path $RunDir $NameDemo
Copy-Item -LiteralPath $formalExe -Destination $formalDest -Force
Copy-Item -LiteralPath $demoExe   -Destination $demoDest   -Force

# 5. Sanity checks: the DEMO binary MUST report the DEMO product name
$formalVI = (Get-Item -LiteralPath $formalDest).VersionInfo
$demoVI   = (Get-Item -LiteralPath $demoDest).VersionInfo
if ($demoVI.ProductName -ne 'SprayerTouchHMI_Demo') {
    throw "DEMO binary has wrong product name '$($demoVI.ProductName)'; DEMO mode would not activate."
}
if ($formalVI.ProductName -ne 'SprayerControl.App') {
    throw "Formal binary has wrong product name '$($formalVI.ProductName)'."
}

# 6. Optional readme sync
if ($ReadmePath -and (Test-Path -LiteralPath $ReadmePath)) {
    Copy-Item -LiteralPath $ReadmePath -Destination (Join-Path $RunDir $NameReadme) -Force
    Write-Host "Readme copied: $ReadmePath" -ForegroundColor Green
}

Write-Host "==> Done." -ForegroundColor Green
Get-ChildItem -LiteralPath $RunDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
