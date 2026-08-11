# Task 1 Brief: Verified source copies and reproducible baseline

## Context

This is the first implementation task for the independent `SprayerTouchHMI_2026-08-12` project. No source copy exists yet and the project is not yet a Git repository. The two external baselines are immutable.

## Read first

- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/AGENTS.md`
- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/PROJECT_CONTEXT.md`
- `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md`

## Immutable inputs

- Desktop: `E:/CodexFILE/Sprayer controller system/SprayerDesktop`
- Firmware: `E:/CodexFILE/Sprayer controller system/SprayerController`

Do not modify timestamps, content, names, or locations in either input.

## Required outputs

- `Tools/Initialize-SourceCopies.ps1`
- `Docs/BASELINE_MANIFEST_DESKTOP.sha256`
- `Docs/BASELINE_MANIFEST_FIRMWARE.sha256`
- `Docs/REQUIREMENTS.md`
- `Docs/DECISIONS.md`
- `Docs/PROGRESS.md`
- `Docs/PROTOCOL.md`
- `Docs/HANDOFF.md`
- `Source/SprayerDesktop/**`
- `Source/SprayerController/**`
- `.gitignore`
- `.superpowers/sdd/progress.md`
- local Git repository with one verified baseline commit and no remote
- full report: `Docs/superpowers/sdd/task-1-report.md`

## Exact behavior

1. Use `apply_patch` for all authored text/script files. File copies may use native PowerShell/robocopy.
2. Copy clean source trees while excluding generated directories named `bin`, `obj`, `build`, `cmake-build-*`, `.git`, and `.vs` at any depth. Do not exclude source, tests, project files, CubeMX files, tool scripts, README files, or IDE/toolchain definitions that are not generated output.
3. The copy script accepts mandatory `DesktopSource`, `FirmwareSource`, and `ProjectRoot`; it refuses any existing non-empty destination source tree.
4. Generate sorted SHA-256 manifest lines in the format `<UPPERCASE_HASH>  <forward/slash/relative/path>` for every included file. Source and copied manifests must match exactly.
5. Record included file counts and aggregate manifest hashes in the report. Re-hash the immutable baselines after copy and prove they match the pre-copy manifests.
6. Run `dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release`; record exact passed/failed totals.
7. Run both existing firmware PowerShell tests under `Source/SprayerController/tools/tests` and record exact results.
8. Probe STM32 CMake/compiler availability without claiming success if unavailable. Do not flash hardware.
9. Populate durable documents with confirmed requirements: English isolated project; two source copies; responsive 16:9/16:10 touch UI; eight independent nozzles; side masters and presets; PWM configurable lower/fixed 80 upper; ten points 10..100; safety minimum; no 0 percent cut; host-local calibration; protocol v2 direction; old projects preserved.
10. `.gitignore` must ignore generated desktop/firmware build output, `Temp`, and local user/IDE state, while keeping `Release`, `Docs`, source, manifests, and review records trackable.
11. Initialize Git only after successful copy/manifest/test evidence. Create one commit with message `chore: establish verified independent sprayer project baseline`. Do not configure a remote.
12. After commit, the worktree must be clean except `.superpowers/sdd/progress.md` if intentionally ignored. Record commit id.

## Report contract

Write Docs/superpowers/sdd/task-1-report.md using apply_patch. Include status (DONE, DONE_WITH_CONCERNS, NEEDS_CONTEXT, or BLOCKED), created files, excluded-directory rules, source/copy file counts, source/copy manifest hashes, baseline before/after proof, test commands and exact output summaries, compiler probe, Git status/remotes, self-review, and concerns. A tracked report cannot embed the final SHA of the commit that contains it; state HEAD as the canonical final reference and record the concrete post-commit SHA in the ignored .superpowers/sdd/progress.md ledger.

Return only status, commit id, one-line verification summary, report path, and concerns.
