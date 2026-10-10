$ErrorActionPreference = 'SilentlyContinue'
Get-ChildItem 'E:\APP\VisualStudio\InstallPlace\VC\Auxiliary\Build' -Recurse -Include 'vcvars64.bat','vcvarsall.bat' | ForEach-Object { Write-Host $_.FullName }
Write-Host '---'
Get-ChildItem 'E:\APP\VisualStudio\InstallPlace\VC\Tools\MSVC' -Recurse -Filter 'cl.exe' -ErrorAction SilentlyContinue | Select-Object -First 5 | ForEach-Object { Write-Host $_.FullName }
Write-Host '---done---'
