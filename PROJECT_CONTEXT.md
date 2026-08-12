# Project Context

## Project

`SprayerTouchHMI_2026-08-12` is the independent development project for a responsive touchscreen sprayer host application and its matching STM32 firmware.

## Immutable source baselines

- Desktop: `E:/CodexFILE/Sprayer controller system/SprayerDesktop`
- Firmware: `E:/CodexFILE/Sprayer controller system/SprayerController`

Do not modify, move, rename, or delete these baselines. Editable copies will be placed under this project's `Source` directory only after manifest verification.

## Current phase

Tasks 3 through 5 are complete in local task commits. Firmware exposes protocol-v2 explicit eight-nozzle control at 0x000C and capabilities at 0x010A..0x010B. The desktop now detects those capabilities with illegal-address-only legacy fallback, writes explicit masks and atomic operating setpoints, handles five-byte serial exception frames, exposes hardware/demo controller sessions, atomically persists schema-1 host configuration, and provides deterministic dashboard/settings/log ViewModels. Task 6 remains the WPF visual layer.

## Baseline checkpoint and migration

- Tools/Initialize-SourceCopies.ps1 performs only a verified first copy. It rejects overlapping baseline/project/destination paths before any filesystem mutation, including reparse-point aliases, and refuses non-empty destination source trees.
- Docs/BASELINE_MANIFEST_DESKTOP.sha256 and Docs/BASELINE_MANIFEST_FIRMWARE.sha256 are the canonical included-file lists. Every manifest file must exist and be Git-tracked below the matching Source/ copy.
- Re-run Tools/tests/Initialize-SourceCopies.Tests.ps1, the desktop test suite, the two firmware PowerShell tests, and the copied-firmware CMake build before declaring a recreated baseline valid. Exact commands and recorded results are in Docs/PROGRESS.md and Docs/superpowers/sdd/task-1-report.md.
- The repository intentionally has no remote. A local ignored ledger at .superpowers/sdd/progress.md records the concrete final HEAD after the self-referential tracked report is committed; on a migrated clone, use git rev-parse HEAD as the canonical final reference.

## Binding requirements summary

- Windows `.NET 8` WPF touchscreen application.
- Responsive landscape layout for 16:9 and 16:10 displays.
- Native WPF recreation of the approved dark three-column HMI.
- Eight individually controllable nozzle channels plus left/right masters and canopy presets.
- PWM master and unified duty; configurable lower bound and fixed `80%` upper bound.
- Return valve uses ten calibration points from `10%` through `100%`, piecewise-linear interpolation, and a configurable safety minimum.
- No special `0%` hardware signal-cut feature.
- Calibration is stored locally on the host; firmware receives mapped current only.
- Old desktop and firmware projects remain unchanged.
- Project documentation must support migration to another Codex account or computer without relying on chat history.

## Primary specification

Read `Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md` before planning or implementation.

## Safety and verification

- Do not invent UI controls without a real desktop, protocol, firmware, or local-configuration implementation.
- Do not claim STM32 build success unless a fresh compiler-backed build ran successfully.
- Controller feedback, not optimistic UI state, is authoritative for nozzle and PWM status.
- Never store credentials in this project.
- Task 3 verification is recorded in Docs/superpowers/sdd/task-3-report.md: native MSVC CTest, both existing firmware PowerShell tests, and a fresh compiler-backed ARM Release build completed without flashing hardware.
- Task 4 verification is recorded in Docs/superpowers/sdd/task-4-report.md: focused Release tests 40/40, full desktop Release tests 135/135, Release build with 0 warnings and 0 errors. No serial hardware or spraying hardware was used.
- Task 5 verification is recorded in Docs/superpowers/sdd/task-5-report.md: focused Release tests 21/21, full desktop Release tests 163/163, Release build with 0 warnings and 0 errors. No serial hardware, UI automation, or spraying hardware was used.
