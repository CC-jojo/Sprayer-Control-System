# Task 3 Brief: Firmware protocol v2 and explicit eight-nozzle mask

## Context

Task 2 was approved at `da13fbbde56f22e92878eb20bd279aedc6545d41`. This task adds the matching STM32 protocol/configuration behavior for explicit eight-channel nozzle control while preserving the reviewed Modbus, PWM, current-module, retry, watchdog, CRC, and legacy-preset behavior. Do not flash hardware.

## Read first

- `AGENTS.md`
- `PROJECT_CONTEXT.md`
- `Docs/REQUIREMENTS.md`
- `Docs/DECISIONS.md`
- `Docs/PROTOCOL.md`
- `Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md`
- Task 3 in `Docs/superpowers/plans/2026-08-12-sprayer-touch-hmi.md`
- Existing firmware files named below, plus `App/host_modbus.c`, `App/pwm_engine.c`, and the root firmware CMake files.

## Editable scope

Product code:

- `Source/SprayerController/App/host_registers.h`
- `Source/SprayerController/App/host_registers.c`
- `Source/SprayerController/App/app_runtime.c`
- `Source/SprayerController/App/config_store.h`
- `Source/SprayerController/App/config_store.c`
- `Source/SprayerController/Platform/app_hal.c`

Tests and durable records:

- `Source/SprayerController/Tests/CMakeLists.txt`
- `Source/SprayerController/Tests/test_host_registers.c`
- `Source/SprayerController/Tests/test_config_store.c`
- `Docs/PROTOCOL.md`
- `Docs/PROGRESS.md`
- `Docs/DECISIONS.md`
- `Docs/HANDOFF.md`
- `PROJECT_CONTEXT.md`
- `Docs/superpowers/sdd/task-3-brief.md`
- `Docs/superpowers/sdd/task-3-report.md`

Do not edit desktop source, CubeMX-generated code, toolchain definitions, download scripts, external baselines, or unrelated files.

## Register and configuration contract

- Extend `SprayMode` by appending `SPRAY_CUSTOM=5`; do not renumber legacy values `0..4`.
- Add `uint8_t nozzle_mask` to `ControllerConfig`. Bit mapping matches desktop Task 2: group 1 left/right bits 0/1, group 2 bits 2/3, group 3 bits 4/5, group 4 bits 6/7.
- Defaults are legacy `SPRAY_ALL` with mask `0xFF`, duty `30`, bounds `30..80`, persistence off, target current `0`.
- Legacy mode masks remain exact: all-off `0x00`, group4 `0xC0`, groups34 `0xF0`, groups234 `0xFC`, all `0xFF`.
- A valid non-custom configuration must carry the matching derived legacy mask; a custom configuration accepts any byte `0x00..0xFF`.
- Writing holding register `0x000C` requires exactly one value in `0x0000..0x00FF`, stores the byte, selects `SPRAY_CUSTOM`, is atomic on error, and is readable at `0x000C`.
- Writing legacy mode register `0x0000` updates `nozzle_mask` to the matching legacy mask. Writing `SPRAY_CUSTOM` through the mode register may retain the current mask, but explicit UI control will use `0x000C`.
- Existing writable addresses, save key, fixed duty bounds, PWM master, target-current limit, and batch atomicity remain unchanged.
- Input registers/holding reads `0x010A` and `0x010B` return protocol version `2` and feature flags `0x0001` (explicit nozzle mask). Existing `0x0100..0x0109` meanings are unchanged.
- `HostRegisters_GetOutputMask` returns the exact custom mask for custom mode and the fixed derived mask for legacy modes.
- Bump the displayed firmware version from `0x0201` to `0x0202` while remaining in the existing discovery-compatible range.

## Flash record compatibility

- Keep `CONFIG_STORE_RECORD_SIZE` exactly `24` bytes and half-word aligned.
- Encode a new schema version `4`, retaining the existing header, payload-size field, byte layout, CRC32 coverage `0..19`, and CRC at bytes `20..23`.
- Use previously reserved byte `19` for `nozzle_mask`; encode the effective mask for every mode.
- Decode both legacy schema version `3` and new version `4`; reject every other version, invalid header/payload/CRC/config, and invalid persistence byte.
- For v3, ignore reserved byte 19 and derive `nozzle_mask` from legacy mode. A v3 record cannot contain custom mode.
- For v4, legacy modes must resolve to their derived masks; custom mode uses byte 19. Preserve CRC validation before trusting payload.
- On every decode failure, return `false` and restore complete defaults, including mask `0xFF`.

## Runtime and hardware documentation

- `ApplyTargets` continues to pass `HostRegisters_GetOutputMask` and duty to `PwmEngine`.
- `AppRuntime_Tick500us` must allow valid custom mask `0x00` without relying on the legacy `mode != SPRAY_ALL_OFF` guard; PWM disabled still forces physical output/readback to zero.
- Status `0x0108` remains authoritative actual PWM-gated output, not merely the command mask.
- Correct the `WriteValveOutputs` comment to state even bits are left nozzles and odd bits are right nozzles. Do not change GPIO pin mappings.

## TDD and host tests

1. Create native C tests first and record a real RED result before product changes.
2. `test_host_registers.c` must cover defaults, all legacy masks, explicit-mask read/write, `0x00`, `0xA5`, `0xFF`, illegal `0x0100`, atomic no-change on error, mode/mask synchronization, capabilities, and unchanged key registers.
3. `test_config_store.c` must cover v4 legacy/custom round trips, all mask edge values, exact 24-byte schema/layout, valid handcrafted v3 decode with derived mask, bad CRC/version/payload/persistence/config rejection, and full-default restoration on every failure.
4. `Tests/CMakeLists.txt` builds portable native C11 test executables from the real App sources, enables CTest, and uses strict warnings (`/W4 /WX` for MSVC or `-Wall -Wextra -Werror` where supported). No duplicated production implementation in tests.
5. On this machine a native MSVC compiler is available under Visual Studio Build Tools even if `cl.exe` is not initially on PATH. Enter its developer environment only for the host-test configure/build; do not change global environment/configuration.
6. Run native configure, build, and CTest in `Temp/firmware-host-tests`; then both existing firmware PowerShell tests; then a clean STM32 Release configure/build with the real ARM compiler. Do not flash.

## Documentation, report, and commit

- Update protocol allocation and compatibility tables, progress/test evidence, decision record, current project phase, and handoff commands. Use `powershell.exe -File` where appropriate on this machine and label bootstrap-only commands accurately.
- Write `Docs/superpowers/sdd/task-3-report.md` with status, start/end refs, changed files, RED evidence, host/firmware test totals, build/toolchain output, protocol/config compatibility table, self-review, and concerns.
- Start from exact HEAD `da13fbbde56f22e92878eb20bd279aedc6545d41`; stop on unexpected tracked changes.
- Commit only after all host tests, existing firmware tool tests, STM32 Release build, and `git diff --check` pass.
- Exact commit message: `feat: add protocol v2 explicit eight-nozzle mask`.
- Return status; commit hash; changed files; exact verification summary; report path; concerns.
