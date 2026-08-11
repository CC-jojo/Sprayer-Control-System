# Task 1 report: verified source copies and reproducible baseline

## Status

DONE

## Created and reviewed deliverables

- Tools/Initialize-SourceCopies.ps1, now with pre-mutation canonical reparse-aware containment checks.
- Tools/tests/Initialize-SourceCopies.Tests.ps1, focused safe temporary-directory tests.
- Docs/BASELINE_MANIFEST_DESKTOP.sha256 and Docs/BASELINE_MANIFEST_FIRMWARE.sha256.
- Docs/REQUIREMENTS.md, Docs/DECISIONS.md, Docs/PROGRESS.md, Docs/PROTOCOL.md, Docs/HANDOFF.md, and PROJECT_CONTEXT.md.
- Source/SprayerDesktop and Source/SprayerController verified copies, .gitignore, and the ignored local .superpowers/sdd/progress.md ledger.

## Copy safety and deterministic manifest behavior

The initializer accepts mandatory DesktopSource, FirmwareSource, and ProjectRoot inputs. Before creating a directory or writing any output, it opens each existing path as a directory handle and resolves its final path. It then rejects:

- overlapping desktop and firmware baseline roots;
- a project root equal to, inside, or containing either immutable baseline;
- every source/destination containment overlap in either direction; and
- overlapping desktop and firmware destinations.

Prospective destinations are resolved from their deepest existing ancestor, so an existing Source junction/reparse point cannot bypass the check. Only after all checks pass does the script reject non-empty destinations and begin copying.

Both canonical destinations must also be strictly contained by canonical ProjectRoot. This prevents a ProjectRoot/Source junction from redirecting a copy to an unrelated directory. Destination emptiness is now checked for both targets without mutation before either missing destination directory is created.

Traversal excludes directories named bin, obj, build, .git, and .vs, plus names matching cmake-build-*, at every depth. It preserves source, tests, CubeMX, tools, README, non-generated IDE, and toolchain files.

Manifest paths are sorted with System.StringComparer.Ordinal by relative path, not culture. Copy/source/baseline-after comparisons are exact ordinal sequence comparisons, not case-insensitive set comparisons. Lines use uppercase SHA-256, two spaces, then a forward-slash relative path.

## Manifest and tracking evidence

| Tree | Source files | Copied files | Git-tracked manifest files | Source/copy aggregate hash |
| --- | ---: | ---: | ---: | --- |
| Desktop | 34 | 34 | 34 | 822C0AFBBBFDA17E428513F65B157AF026CE55CA2CC8D06F81CA37BDABFF8EAA |
| Firmware | 129 | 129 | 129 | 1F35F130128E3C0E89BF8AD41A7ACA0BC47BD43F694A19B9F4928D7CE4AE019A |

Aggregate hashes are SHA-256 of the canonical UTF-8, LF-terminated manifest content. Post-review verification re-hashed both immutable baselines and copied trees; stored manifests, source manifests, and copied manifests matched exactly by ordinal sequence. Every one of the 34 desktop and 129 firmware manifest paths exists below Source and is Git-tracked. The seven firmware .idea files, including the portable STM32 download run configuration, are intentionally tracked because they are included in the verified baseline manifest.

## Verification

| Check | Command | Exact summary | Result |
| --- | --- | --- | --- |
| Copy-script safety/determinism | Tools/tests/Initialize-SourceCopies.Tests.ps1 | 7 refusal-without-mutation cases passed: inside baseline, equal baseline, junction alias, overlapping baselines, source/destination containment, ProjectRoot/Source escape junction, and first-destination creation blocked by a non-empty second destination; ordinal output was identical under sv-SE and default cultures | PASS |
| Desktop baseline tests | dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release --logger console;verbosity=minimal | Failed: 0; passed: 38; skipped: 0; total: 38; duration: 34 ms | PASS |
| Firmware output-format test | Source/SprayerController/tools/tests/test-stm32-download-output.ps1 | PASS: STM32 download output uses ASCII progress bars | PASS |
| Firmware retry test | Source/SprayerController/tools/tests/test-stm32-download-retry.ps1 | PASS: STM32 download retries and recovers before programming | PASS |
| Firmware toolchain | cmake --version; arm-none-eabi-gcc --version | CMake 3.28.1 and GNU Tools for STM32 arm-none-eabi-gcc 13.3.1 available | PASS |
| Firmware Release build | cmake --preset Release; cmake --build --preset Release --clean-first | Fresh build cleaned 35 files, rebuilt all 35 Ninja steps, and linked SprayerController.elf; RAM 2072 B / 20 KB (10.12%); FLASH 10388 B / 63 KB (16.10%) | PASS |

No hardware was flashed. The firmware build ran only inside the copied source tree.

## Git baseline

- Commit message: chore: establish verified independent sprayer project baseline.
- Canonical final reference: HEAD. Run git rev-parse HEAD in this repository or a migrated clone.
- A tracked report cannot contain its own final commit SHA: changing that text changes the SHA. Therefore the ignored local ledger at .superpowers/sdd/progress.md records the concrete post-commit HEAD, exact clean status, and no-remotes result after the final amend.
- The final repository has exactly one local commit and no remote.

## Self-review

- Path safety validation, strict canonical destination containment, and both empty-destination checks run before any New-Item, file copy, or manifest write.
- The .idea exclusion was removed; all manifest-listed baseline files are now clone-reproducible and tracked.
- PROJECT_CONTEXT.md and handoff/progress documentation describe the verified baseline milestone, safety checks, and migration path.

## Concerns

- No physical controller, programmer, or hardware flashing was used; real-device acceptance remains future work.
- The 80 percent PWM upper bound and host-local calibration requirements are documented decisions, not yet implemented product behavior.
