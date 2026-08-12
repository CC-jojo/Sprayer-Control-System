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
- `Controls/NozzleArrayControl` exposes confirmed and preview byte masks, pending state, and an `ICommand`; button parameters are `NozzleCommandParameter` values with one-based `Group` and `Right` members. The fixed bit mapping remains even=left and odd=right.
- `Controls/SegmentedGaugeControl` previews locally while dragged and calls `ReleaseCommand` once only after a completed mouse or touch gesture. Lost capture cancels without a command.
- `Controls/TouchNumericKeypad` is touch-only: it displays a sanitized decimal string and exposes confirm/cancel commands; it contains no text-entry control or physical-input handler.
- Task 6 intentionally leaves the legacy MainWindow untouched. Task 7 must compose the controls into the approved responsive full-bleed three-column dashboard/settings/log views and perform visual-resolution review.
