$ErrorActionPreference = 'Stop'
$root = 'E:\CodexFILE\SprayerTouchHMI_2026-08-12\Source\SprayerController'
$buildDir = Join-Path $root 'Tests\build-verify'

if (Test-Path $buildDir) { Remove-Item -Recurse -Force $buildDir }

$vcvars = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat'

$cmd = "call `"$vcvars`" >nul 2>&1 && cmake -S `"$root\Tests`" -B `"$buildDir`" -G `"NMake Makefiles`" -DCMAKE_BUILD_TYPE=Debug && cmake --build `"$buildDir`" && ctest --test-dir `"$buildDir`" --output-on-failure"
cmd /c $cmd
$code = $LASTEXITCODE
Write-Host "EXIT CODE: $code"
exit $code
