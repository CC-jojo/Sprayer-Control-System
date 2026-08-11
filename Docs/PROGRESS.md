# Progress

## Completed

- Verified clean copies of the two immutable baselines were created in Source/ with excluded generated output removed.
- Docs/BASELINE_MANIFEST_DESKTOP.sha256 and Docs/BASELINE_MANIFEST_FIRMWARE.sha256 record the included-file baseline.
- Existing desktop and firmware tests ran against the copied source; details are in Docs/superpowers/sdd/task-1-report.md.
- A copied-firmware Release CMake build completed. No hardware was flashed.
- Copy initialization was security-reviewed and hardened: canonical reparse-aware path overlap checks run before any directory creation, manifests use ordinal relative-path ordering and ordinal exact comparison, and all manifest-listed source files are Git-tracked.
- Focused PowerShell safety/determinism tests are available at Tools/tests/Initialize-SourceCopies.Tests.ps1.

## Current

The project is at a clean, committed baseline. No product behavior, UI, protocol register allocation, or firmware logic has yet been changed from the copied baseline.

## Next

1. Add focused tests for eight-bit masks, side masters, canopy presets and custom patterns before behavior changes.
2. Specify and test the protocol-v2 register/feature negotiation while retaining baseline compatibility.
3. Introduce host-local calibration validation, atomic persistence and return-valve mapping.
4. Implement the responsive WPF dashboard and settings views with controller-confirmed state transitions.

## Verification commands

The initialization command below is for a fresh bootstrap or deliberate recreation into an empty project only. Do not run it against this completed repository: it correctly refuses non-empty Source destinations.

### Fresh bootstrap or recreation only

    pwsh -File Tools/Initialize-SourceCopies.ps1 -DesktopSource 'E:/CodexFILE/Sprayer controller system/SprayerDesktop' -FirmwareSource 'E:/CodexFILE/Sprayer controller system/SprayerController' -ProjectRoot (Get-Location)

### Existing completed repository

    dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release
    pwsh -File Source/SprayerController/tools/tests/test-stm32-download-output.ps1
    pwsh -File Source/SprayerController/tools/tests/test-stm32-download-retry.ps1
    cmake --preset Release
    cmake --build --preset Release

The initialization script intentionally refuses non-empty Source/SprayerDesktop or Source/SprayerController destinations.
