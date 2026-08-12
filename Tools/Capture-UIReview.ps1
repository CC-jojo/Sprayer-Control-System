param(
    [string]$Configuration = 'Release',
    [string]$OutputDirectory = 'Temp/UIReview'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$app = Join-Path $projectRoot "Source/SprayerDesktop/SprayerControl.App/bin/$Configuration/net8.0-windows/SprayerControl.App.exe"
if (-not (Test-Path $app)) { throw "Build the Release application before capture: $app" }
$target = Join-Path $projectRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $target | Out-Null
Add-Type -AssemblyName System.Drawing

$sizes = @(@(1366,768), @(1600,900), @(1920,1080), @(1280,800), @(1920,1200), @(1536,864))
foreach ($size in $sizes) {
    $width, $height = $size
    $file = Join-Path $target ("dashboard-{0}x{1}.png" -f $width,$height)
    Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
    $arguments = @('--demo', '--capture', ('"{0}"' -f $file), '--width', [string]$width, '--height', [string]$height)
    $process = Start-Process -FilePath $app -ArgumentList $arguments -PassThru -Wait
    try {
        if ($process.ExitCode -ne 0 -or -not (Test-Path $file)) { throw "Application capture failed for $width x $height (exit $($process.ExitCode))." }
        $image = [System.Drawing.Image]::FromFile($file)
        try { if ($image.Width -ne $width -or $image.Height -ne $height -or (Get-Item $file).Length -eq 0) { throw "PNG verification failed: $file" } } finally { $image.Dispose() }
    } finally { }
}
Get-ChildItem $target -Filter '*.png' | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    "{0}  {1}" -f $hash, $_.FullName.Substring($projectRoot.Length + 1).Replace('\','/')
}
