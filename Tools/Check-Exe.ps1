$ErrorActionPreference = 'SilentlyContinue'
$base = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12'
$releaseDir = Join-Path $base (-join ([char[]](0x8FD0,0x884C,0x7248,0x672C)))
Get-ChildItem -LiteralPath $releaseDir -Filter '*.exe' | ForEach-Object {
    $vi = $_.VersionInfo
    Write-Host "EXE: $($_.Name)"
    Write-Host "  ProductName : $($vi.ProductName)"
    Write-Host "  FileVersion : $($vi.FileVersion)"
    Write-Host "  Modified    : $($_.LastWriteTime)"
    Write-Host ""
}
Write-Host '---done---'
