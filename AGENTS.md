# Project Rules

## Start here

Read these files before changing source:

1. `PROJECT_CONTEXT.md`
2. `Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md`
3. `Docs/REQUIREMENTS.md` when it exists
4. `Docs/DECISIONS.md` when it exists
5. `Docs/PROGRESS.md` when it exists
6. `Docs/PROTOCOL.md` when it exists

## Source boundaries

- Only edit source below this project's `Source` directory.
- The external desktop and firmware baselines named in `PROJECT_CONTEXT.md` are read-only.
- Preserve unrelated user changes.
- Use English ASCII names for all source paths and toolchain-sensitive files.

## Implementation rules

- Keep the WPF application on `.NET 8` unless an approved decision changes it.
- Preserve proven serial discovery, Modbus RTU, retry, reconnect, PWM, current-module, watchdog, and CRC behavior.
- Every visible control must map to implemented host logic, protocol behavior, firmware behavior, or an explicitly local setting.
- Use controller readback as the authoritative equipment state.
- Add tests before behavior changes and record commands/results in `Docs/PROGRESS.md`.
- Update `PROJECT_CONTEXT.md`, `Docs/DECISIONS.md`, `Docs/PROGRESS.md`, `Docs/PROTOCOL.md`, and `Docs/HANDOFF.md` at reviewed milestones.

## Portability and secrets

- Prefer relative paths inside the project.
- Do not store API keys, account tokens, passwords, private certificates, or user-specific authentication data.
- Release packages must pair desktop and firmware versions and include checksums.
