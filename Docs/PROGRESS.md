# Progress

## Completed

- Verified clean copies of the two immutable baselines were created in Source/ with excluded generated output removed.
- Docs/BASELINE_MANIFEST_DESKTOP.sha256 and Docs/BASELINE_MANIFEST_FIRMWARE.sha256 record the included-file baseline.
- Existing desktop and firmware tests ran against the copied source; details are in Docs/superpowers/sdd/task-1-report.md.
- A copied-firmware Release CMake build completed. No hardware was flashed.
- Copy initialization was security-reviewed and hardened: canonical reparse-aware path overlap checks run before any directory creation, manifests use ordinal relative-path ordering and ordinal exact comparison, and all manifest-listed source files are Git-tracked.
- Focused PowerShell safety/determinism tests are available at Tools/tests/Initialize-SourceCopies.Tests.ps1.
- Task 3 added firmware protocol v2: 0x000C writes an explicit eight-bit nozzle mask, 0x010A..0x010B advertise version 2 and feature bit 0x0001, and firmware version is 0x0202.
- Task 3 preserves legacy mode masks and existing register behavior, adds Flash schema v4 while decoding schema v3, and keeps 0x0108 as physical PWM-gated output readback.
- Task 4 added desktop protocol-v2 capability detection, explicit-mask and atomic operating-setpoint writes, typed Modbus exception codes, and five-byte serial exception-frame handling.
- Task 4 added cached Modbus and deterministic demo controller sessions. The demo keeps commanded mask separate from PWM-gated actual readback, supports cancellation and one-shot failure injection, and is disposable.
- Task 5 added schema-1 validated host-local JSON persistence. Saves serialize on a store instance, flush a unique same-directory UTF-8 temporary file, then atomically replace/move; failures and cancellation clean the temporary file without changing the preceding target.
- Task 5 added observable/async-command, complete-plus-recent-five logging, dashboard preview/pending/confirmed state, and settings editing. Dashboard nozzle state is refreshed from controller output-mask readback; setpoint changes remain local until commit and map calibration mA to microamps with midpoint-away-from-zero rounding.
- Task 6 added merged dark-HMI resource dictionaries, shared cards/headings/touch-status/focus-disabled-pending styles, and native reusable nozzle-array, segmented-gauge, and touch-only numeric-keypad controls. These controls are not yet inserted into MainWindow or final views.
- Task 6 review remediation renders pending nozzle state from the complete preview target, blocks duplicate nozzle/gauge interactions while pending, clamps gauge preview/release to the configured safety floor, and adds dynamic Chinese automation semantics to nozzles and gauges.

## Current

Desktop local configuration, interaction state, and reusable WPF controls are ready for Task 7 view integration. Hardware flashing, serial-device verification, UI automation, and physical-output acceptance have not been performed.

## Next

1. Implement the responsive WPF dashboard, settings, and full-log views bound to the Task 5 ViewModels and Task 6 reusable controls.
2. Perform separately authorized real-controller acceptance after the UI is integrated.

## Verification commands

The initialization command below is for a fresh bootstrap or deliberate recreation into an empty project only. Do not run it against this completed repository: it correctly refuses non-empty Source destinations.

### Fresh bootstrap or recreation only

    pwsh -File Tools/Initialize-SourceCopies.ps1 -DesktopSource 'E:/CodexFILE/Sprayer controller system/SprayerDesktop' -FirmwareSource 'E:/CodexFILE/Sprayer controller system/SprayerController' -ProjectRoot (Get-Location)

### Existing completed repository

    dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Source/SprayerController/tools/tests/test-stm32-download-output.ps1
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Source/SprayerController/tools/tests/test-stm32-download-retry.ps1
    cmake --fresh --preset Release
    cmake --build --preset Release --target clean
    cmake --build --preset Release

### Task 3 evidence (no hardware flashing)

    cmd.exe /d /s /c "call \"C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\Tools\VsDevCmd.bat\" -arch=x64 && cmake -S .\Source\SprayerController\Tests -B .\Temp\firmware-host-tests -G Ninja && cmake --build .\Temp\firmware-host-tests --config Release && ctest --test-dir .\Temp\firmware-host-tests -C Release --output-on-failure"

- Native MSVC CTest: 2/2 passed; strict `/W4 /WX /utf-8` was used.
- Existing firmware PowerShell tests: 2/2 passed.
- Fresh ARM Release build: GNU Arm Embedded Toolchain 13.3.1, 35/35 Ninja steps completed; RAM 2072 B/20 KB and Flash 10540 B/63 KB. No `flash` target was invoked.

The initialization script intentionally refuses non-empty Source/SprayerDesktop or Source/SprayerController destinations.

### Task 4 evidence (no serial hardware)

    dotnet test Source/SprayerDesktop/SprayerControl.Tests/SprayerControl.Tests.csproj --configuration Release --filter 'FullyQualifiedName~ControllerClientTests|FullyQualifiedName~ControllerCapabilitiesTests|FullyQualifiedName~ControllerSessionTests|FullyQualifiedName~ModbusRtuCodecTests|FullyQualifiedName~ControllerSettingsTests'
    dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release
    dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release
    git diff --check

- RED was recorded before product edits: the new session test failed to compile with expected `CS0234` because `SprayerControl.App.Services` did not yet exist.
- Focused Release tests: 40/40 passed.
- Full desktop Release tests: 135/135 passed.
- Release build: 0 warnings, 0 errors. `git diff --check` completed successfully.

### Task 5 evidence (no serial/UI/hardware)

    dotnet test Source/SprayerDesktop/SprayerControl.Tests/SprayerControl.Tests.csproj --configuration Release --filter 'FullyQualifiedName~JsonHostConfigurationStoreTests|FullyQualifiedName~DashboardViewModelTests|FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~LogViewModelTests'
    dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release
    dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release
    git diff --check

- RED: new Task 5 tests failed to compile with expected `CS0234` because the ViewModels namespace and configuration-store interface did not exist.
- Review RED: schema-validation regression failed 1/1 because the `InvalidDataException` did not preserve an inner cause.
- Focused Release tests: 21/21 passed.
- Full desktop Release tests: 163/163 passed.
- Release build: 0 warnings, 0 errors. `git diff --check` completed successfully.

### Task 6 evidence (no serial/UI/hardware)

    dotnet test Source/SprayerDesktop/SprayerControl.Tests/SprayerControl.Tests.csproj --configuration Release --filter 'FullyQualifiedName~MainWindowLayoutTests'
    dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release
    dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release
    git diff --check

- RED: the two new resource/control invariant tests failed with expected missing `Themes/Colors.xaml` and `Controls/NozzleArrayControl.xaml` paths before product files were created.
- Focused Release layout/control tests: 6/6 passed.
- Full desktop Release tests: 166/166 passed.
- Release build: 0 warnings, 0 errors. `git diff --check` completed successfully.
- Task 7 owns replacement of the legacy MainWindow and therefore the final three-column/full-bleed/no-main-screen-TextBox assertions and visual-resolution review.
