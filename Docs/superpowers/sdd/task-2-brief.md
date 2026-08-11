# Task 2 Brief: Desktop domain model for nozzle masks and return-valve calibration

## Context

Task 1 established the reviewed independent baseline at commit `0ad19bd18559708d77b8fb415abdfb7d276bd7fb`. This task adds only pure desktop-domain behavior. It must not change Modbus, WPF, firmware, local file I/O, or the immutable external baselines.

## Read first

- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/AGENTS.md`
- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/PROJECT_CONTEXT.md`
- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/REQUIREMENTS.md`
- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/DECISIONS.md`
- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md`
- Task 2 in `Docs/superpowers/plans/2026-08-12-sprayer-touch-hmi.md`

## Editable scope

- `Source/SprayerDesktop/SprayerControl.Core/NozzleMask.cs`
- `Source/SprayerDesktop/SprayerControl.Core/ReturnValveCalibration.cs`
- `Source/SprayerDesktop/SprayerControl.Core/HostConfiguration.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/NozzleMaskTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/ReturnValveCalibrationTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/HostConfigurationTests.cs`
- `Docs/superpowers/sdd/task-2-brief.md`
- `Docs/superpowers/sdd/task-2-report.md`

Do not edit other product files. A minimal project-file change is allowed only if compilation proves it is required; SDK-style wildcard compilation should make it unnecessary.

## Required interfaces and behavior

### Nozzle mask

- Define `CanopyPreset` with `Group4`, `Groups34`, `Groups234`, and `AllGroups`.
- Define immutable `readonly record struct NozzleMask(byte Value)` with `None`, `All`, `IsEnabled`, `WithNozzle`, `WithLeft`, `WithRight`, and `ForPreset`.
- Fixed bit mapping: group 1 left/right = bits 0/1; group 2 = bits 2/3; group 3 = bits 4/5; group 4 = bits 6/7.
- `group` is one-based and must be `1..4`; reject invalid groups with `ArgumentOutOfRangeException` even when disabling.
- Left master affects even bits (`0x55`); right master affects odd bits (`0xAA`). Disabling a side preserves the opposite side; enabling a side enables all four channels on that side.
- Preset base masks: `Group4=0xC0`, `Groups34=0xF0`, `Groups234=0xFC`, `AllGroups=0xFF`. Apply left/right enable flags after selecting the base mask.
- Test individual channels, every preset, every side-flag combination, invalid groups, value equality, and preservation of unaffected bits.

### Return-valve calibration

- Define immutable `CalibrationPoint(int OpeningPercent, double CurrentMilliamps)` and `ReturnValveCalibration`.
- Accept exactly one point for each opening `10,20,...,100`; constructor input order may vary, but the exposed `Points` collection must be an immutable snapshot sorted ascending by opening.
- Reject null input, wrong count, missing or duplicate openings, openings outside the required sequence, NaN/infinite current, and current outside inclusive `0..20 mA`.
- Current values must be monotonic nondecreasing or monotonic nonincreasing across opening. Equal adjacent/all-equal values are valid. Reject any direction reversal.
- `CurrentMilliampsAt(double openingPercent)` supports exact points and piecewise-linear interpolation on the closed domain `10..100`; reject NaN/infinite or out-of-domain openings.
- `CurrentMilliampsAt(double openingPercent, int safetyMinimumOpening)` additionally requires a safety minimum in `10..100` and rejects requested opening below that minimum with `InvalidOperationException`. It uses the same interpolation otherwise.
- Returned/public collections must not allow caller mutation of internal state.

### Host configuration

- Define immutable `HostConfiguration` with `SchemaVersion`, `PwmDutyMinimum`, `ReturnValveSafetyMinimum`, `ReturnValveCalibration`, and `LastModifiedUtc`.
- It must make and expose an immutable calibration snapshot, not retain a caller-owned mutable list.
- Reject schema versions below `1`; PWM minimum outside inclusive `30..80`; return safety minimum outside inclusive `10..100`; default/unset `LastModifiedUtc`; null or invalid calibration.
- Fixed PWM maximum remains `80` and is not a configurable field.
- Host configuration validation must reuse the same calibration invariants rather than duplicating weaker rules.
- This task does not select a default ten-point current table and does not add file persistence.

## TDD and verification

1. Add the focused tests first and run them to prove RED for missing behavior. Record the exact failing command/summary in the report.
2. Implement the smallest production code that satisfies the tests.
3. Run the focused tests, then the full desktop suite in Release.
4. Run `git diff --check` and inspect the diff for scope, public API clarity, culture independence, finite-number validation, and immutability leaks.
5. Do not weaken or delete baseline tests.

## Commit and report

- Start from exact baseline HEAD `0ad19bd18559708d77b8fb415abdfb7d276bd7fb`; stop if tracked changes already exist outside this task brief.
- Commit only after all desktop tests pass.
- Commit message: `feat: add nozzle mask and return-valve calibration domain`.
- Write `Docs/superpowers/sdd/task-2-report.md` before committing. Include status, start/end commit references (use canonical `HEAD` for the self-referential end), changed files, RED evidence, GREEN/full-suite exact totals, behavior table, self-review, and concerns.
- Return `DONE`, `DONE_WITH_CONCERNS`, `BLOCKED`, or `NEEDS_CONTEXT`; commit hash; changed files; exact test summary; report path; concerns.
