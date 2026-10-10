$ErrorActionPreference = 'SilentlyContinue'
Get-ChildItem 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC' -Recurse -Include 'vcvars64.bat','vcvarsall.bat','cl.exe' -ErrorAction SilentlyContinue | Select-Object -First 10 | ForEach-Object { Write-Host $_.FullName }
Write-Host '---done---'
