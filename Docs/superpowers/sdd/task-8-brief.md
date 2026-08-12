# Task 8 Brief: Integrated Verification and Paired Release

## Goal

Produce a reproducible paired desktop/firmware release for the completed touchscreen sprayer system, with portable migration documentation and evidence-backed verification.

## Required work

1. Run the full desktop Release test suite and publish `win-x64`, framework-dependent output to `Release/Desktop`.
2. Run native firmware CTest, both firmware PowerShell test scripts, and a clean ARM Release build. Package only freshly verified `.elf`, `.hex`, and `.bin` files into `Release/Firmware`.
3. Preserve the six exact-pixel UI review captures and verify the capture command remains repeatable.
4. Set one paired release version in `Release/VERSION.txt` and document the exact desktop/firmware source commit.
5. Update `PROJECT_CONTEXT.md`, `Docs/REQUIREMENTS.md`, `Docs/DECISIONS.md`, `Docs/PROGRESS.md`, `Docs/PROTOCOL.md`, and `Docs/HANDOFF.md` with current behavior, exact verification commands/counts, relative paths, release contents, and the first command for migration to a new account/computer.
6. Generate an ASCII `Release/SHA256SUMS.txt` covering every release file except the checksum file itself, using forward-slash relative paths. Verify every listed hash.
7. Scan tracked source/docs/release text for credential-like material. Do not add credentials or machine-private secrets.
8. Leave the worktree clean and commit with the exact message `release: package paired sprayer touch HMI system`.

## Acceptance rules

- The completed reference-faithful layout and color palette must not regress.
- Desktop tests must all pass in Release; publish must complete without warnings/errors.
- Firmware artifacts must come only from the fresh verified ARM build. If the compiler is unavailable, package no stale firmware and document the blocker.
- Demo-mode acceptance may be automated. Real controller, touchscreen, and spraying-hardware acceptance must be explicitly recorded as not performed unless physical hardware is actually available; never imply otherwise.
- The release must pair compatible desktop and firmware artifacts and document protocol/capability expectations.
- Do not modify or delete either original baseline project.
- Avoid unrelated features or refactors.

## Evidence to report

- Exact commit hash before and after the release commit.
- Desktop test totals and publish result.
- Native CTest, PowerShell tool-test, and ARM CTest totals.
- Firmware flash/RAM size from the fresh build.
- Release file inventory plus successful checksum verification.
- Credential scan result and clean-worktree proof.
