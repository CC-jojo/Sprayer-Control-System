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
