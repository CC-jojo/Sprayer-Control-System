# Handoff and migration checklist

## What is portable

- The independent project root, source copies, baseline manifests, specifications, durable documentation and local Git history are sufficient to continue this task on another Codex account or computer.
- No remote is configured and no account, credential, token, certificate or machine secret is stored here.

## Before changing code

1. Read AGENTS.md, PROJECT_CONTEXT.md, Docs/REQUIREMENTS.md, Docs/DECISIONS.md, Docs/PROGRESS.md, and Docs/PROTOCOL.md.
2. Confirm the external baseline paths still exist; never edit either baseline.
3. Check git status --short --branch and preserve unrelated changes.
4. Read the baseline manifests and the task-1 report before comparing source.
5. Run git rev-parse HEAD to identify the canonical committed baseline. The concrete local post-commit value is also recorded in the ignored .superpowers/sdd/progress.md ledger.

## Required tools

- Windows PowerShell or PowerShell 7
- .NET SDK 8 for desktop build/test
- CMake, Ninja and arm-none-eabi-gcc for firmware configure/build
- STM32 hardware/programmer only for separately authorized hardware acceptance; this task did not flash hardware
- Visual Studio Build Tools (MSVC) for portable native firmware tests; enter `VsDevCmd.bat` only for the host-test configure/build command.

## Verification before handoff

Run the commands in Docs/PROGRESS.md, record exact outputs in that milestone's progress/report file, and do not claim firmware success without a fresh compiler-backed build. Pair release desktop and firmware artifacts by version and checksum.

For Task 3, run the two existing firmware checks with `powershell.exe -File`, then run the native CTest command and `cmake --fresh --preset Release` / `cmake --build --preset Release --target clean` / `cmake --build --preset Release` from `Source/SprayerController`. The fresh configure/build is compiler-only verification; do not invoke the separately defined `flash` target without explicit hardware authorization.

When recreating a baseline, first run Tools/tests/Initialize-SourceCopies.Tests.ps1. The copy script intentionally rejects any baseline/project/destination overlap, including a path that resolves into a baseline through a junction or other reparse point.

## Task 4 desktop protocol/session checkpoint

- Use `ModbusControllerSession.CreateAsync(client, ownedLifetime)` after discovery. It reads and caches capabilities once; give it the channel/connection lifetime only when the session owns disposal.
- Explicit nozzle masks require feature bit `0x0001`. Legacy capability fallback is only Modbus exception code `0x02`; all other protocol and transport failures must remain visible.
- `DemoControllerSession` is the hardware-free session for later UI work. It starts as protocol v2, delays confirmation through an injectable delegate, supports `FailNextOperation`, and returns a PWM-gated output mask.
- Re-run the Task 4 commands in Docs/PROGRESS.md before making follow-on UI changes. The detailed evidence and unresolved hardware limitation are in Docs/superpowers/sdd/task-4-report.md.

## Task 5 local configuration/ViewModel checkpoint

- `JsonHostConfigurationStore` defaults to `%LocalAppData%/SprayerTouchHMI/host-configuration.json`; it accepts an explicit file path and replacement delegate for deterministic tests. It accepts schema 1 only and never stores credentials.
- The store creates and flushes a same-directory unique `.tmp` file. Existing targets use `File.Replace`; new targets use same-directory `File.Move`. A failed/cancelled save removes the temporary file and leaves the old target intact.
- `DashboardViewModel` uses controller status output-mask readback as confirmed nozzle state. Mask targets are derived only after its shared session gate is acquired, so queued operations compose from the latest readback. Setpoint commit snapshots duty/opening together inside that same gate, sends exactly that mapped pair, and promotes exactly that snapshot; a later local preview remains unconfirmed for a later commit.
- `SettingsViewModel` starts with no invented calibration values. It needs exactly ten 10..100-point currents before it can save a new immutable schema-1 configuration with a UTC timestamp. `LogViewModel` retains all records and exposes a live newest-five view.

## Task 6 WPF control checkpoint

- `App.xaml` merges `Themes/Colors.xaml` then `Themes/Controls.xaml`. Reuse the stable HMI brush/style keys rather than introducing inline palette values in controls or views.
- `Controls/NozzleArrayControl` exposes confirmed and preview byte masks, pending state, and previewing state. Its eight rings are pure status indicators (never hit-testable or focusable, no command binding); the fixed bit mapping remains even=left and odd=right.
- `Controls/SegmentedGaugeControl` previews locally while dragged and calls `ReleaseCommand` once only after a completed mouse or touch gesture. Lost capture cancels without a command.
- `Controls/TouchNumericKeypad` is touch-only: it displays a sanitized decimal string and exposes confirm/cancel commands; it contains no text-entry control or physical-input handler.
- Task 6 intentionally leaves the legacy MainWindow untouched. Task 7 must compose the controls into the approved responsive full-bleed three-column dashboard/settings/log views and perform visual-resolution review.

## Task 7 shell/view checkpoint

- Dashboard now follows the approved reference palette and compact 40/24/36 composition with circular 4x2 nozzles, canopy ladder, 240-degree gauges, authoritative 4x2 readback and five timestamped log rows.
- `Tools/Capture-UIReview.ps1` produces exact-pixel screenshots at six sizes under `Temp/UIReview`; hashes and inspection findings are in `Docs/superpowers/sdd/task-7-report.md`.
- Settings includes real curve preview/mapped current, confirmed safety-minimum edits, demo persistence blocking and a real separately confirmed controller power-on-default path.

- `MainWindow` starts full-bleed/maximized by default. Use `--demo --windowed --width 1366 --height 768` for hardware-free interaction and `--demo --capture --width 1366 --height 768` for capture tooling.
- `Tools/Capture-UIReview.ps1` produces the six required PNG names under ignored `Temp/UIReview` and validates their PNG dimensions. See `Docs/superpowers/sdd/task-7-report.md` for the exact hashes and the local high-DPI capture limitation.
- `DashboardView`, `SettingsView`, and `LogView` are native WPF views. Settings is intentionally usable with a disconnected controller; local save updates the active dashboard without restart. Demo calibration is visibly marked and never saved as a product default.

## Release 1.0.0 and first migration command

The first command after copying or cloning this project on a new account/computer is, from the project root:

    Get-Content -Raw .\AGENTS.md

Then read `PROJECT_CONTEXT.md`, run `git status --short --branch`, and use `git rev-parse HEAD` to identify the local committed state. The portable release is `Release/`: `Desktop/` is a framework-dependent .NET 8 `win-x64` publish; `Firmware/` contains the freshly built `SprayerController.elf`, `.hex`, and `.bin`; `VERSION.txt` is `1.0.0`; and `SHA256SUMS.txt` verifies every release file except itself.

Release `1.0.0` pairs source commit `be19ee618c9a80a9718147ff760b416972c05502`, firmware `0x0202`, protocol v2 and feature flag `0x0001`. Install .NET 8 Windows Desktop Runtime on the target PC before launching `Release/Desktop/SprayerControl.App.exe`. Do not invoke the firmware `flash` target or energize outputs without separately authorized physical safety setup. Physical serial controller, touchscreen, STM32 flashing and spraying-output acceptance remain pending.

## Task 9 interaction-fix checkpoint and next run method

The 2026-08-12 interaction fix removed the misleading dashboard behavior: the eight nozzle rings are pure status indicators and canopy presets moved from four buttons to a vertical four-position snapping slider. Start any follow-on work by reviewing the dashboard from the UI rather than only reading code:

    dotnet run --project Source/SprayerDesktop/SprayerControl.App/SprayerControl.App.csproj --configuration Release -- --demo --windowed

- The demo session is hardware-free and starts windowed. In the dashboard, drag the canopy-height slider: the eight rings preview the selected pattern while dragging, and one `ApplyCanopyPresetAsync` is sent when the mouse or touch is released.
- From the publish/output directory, `Start-Demo.cmd` launches the same `--demo --windowed` mode without typing commands.
- `DashboardViewModel.ToggleNozzleAsync` remains available as a protocol/test capability; the dashboard UI no longer binds individual nozzle commands.
- Re-run the Task 9 evidence commands in Docs/PROGRESS.md (focused `MainWindowLayoutTests`, full desktop Release tests, Release build, `git diff --check`) before making further changes. Physical serial controller, touchscreen and spraying-output acceptance remain pending.
