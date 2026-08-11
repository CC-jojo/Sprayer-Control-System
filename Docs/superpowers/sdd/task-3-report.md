# Task 3 Report: Firmware protocol v2 explicit eight-nozzle mask

## Status and references

- Status: completed and committed after all required non-hardware verification.
- Start reference: `da13fbbde56f22e92878eb20bd279aedc6545d41`.
- End reference: the commit that contains this report; after checkout, use `git rev-parse HEAD` as the canonical self-referential final reference.
- No external desktop or firmware baseline was edited. No flash operation was invoked.

## Changed files

- Firmware register/config/runtime implementation: `App/host_registers.h`, `App/host_registers.c`, `App/config_store.c`, and `App/app_runtime.c`.
- Hardware mapping documentation: `Platform/app_hal.c`.
- Portable native tests: `Tests/CMakeLists.txt`, `Tests/test_host_registers.c`, and `Tests/test_config_store.c`.
- Durable records: `PROJECT_CONTEXT.md`, `Docs/PROTOCOL.md`, `Docs/DECISIONS.md`, `Docs/PROGRESS.md`, `Docs/HANDOFF.md`, this report, and the task brief.

## TDD evidence

Tests were authored before product edits. Native MSVC RED was observed from `Temp/firmware-host-tests`: `test_host_registers.c` failed because the baseline had neither `ControllerConfig.nozzle_mask` nor `SPRAY_CUSTOM`; config tests failed for the same missing contract. This was the expected missing-feature failure. The baseline header encoding warning was removed from the strict test path with MSVC `/utf-8`; the test build retains `/W4 /WX`.

After the smallest product implementation, the same native build completed and CTest reported 2/2 passed.

### Review-fix TDD evidence

The Task 3 review identified undefined behavior for malformed negative `SprayMode` values. New native tests were added first for negative and above-range modes, safe legacy-mask fallback, invalid-config rejection, and rejection by `ConfigStore_Encode`. The RED run stopped at `!ControllerConfig_IsValid(&config)` for `(SprayMode)-1`, proving that the former upper-bound-only check accepted a malformed value. The process was terminated after the assertion because the MSVC assert dialog kept the test executable alive. The minimal GREEN fix validates lower and upper enum bounds before lookup; `ControllerConfig_GetLegacyMask` deterministically returns disabled mask `0x00` for Custom or invalid values. The fresh MSVC CTest run then reported 2/2 passed.

The same native test executable now drives real `host_modbus.c` and `modbus_crc.c` wire frames for function 0x06 and 0x10 writes to 0x000C, function 0x03 reads of 0x000C and 0x010A count 2, and the 0x0100 invalid-mask exception with atomic unchanged state.

## Verification totals and toolchains

| Check | Command/result | Total |
| --- | --- | --- |
| Native host tests | MSVC 19.51.36248.0, CMake/Ninja configure/build, `ctest --test-dir Temp/firmware-host-tests -C Release --output-on-failure` | 2/2 passed |
| Existing download-output test | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Source/SprayerController/tools/tests/test-stm32-download-output.ps1` | 1/1 passed |
| Existing download-retry test | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Source/SprayerController/tools/tests/test-stm32-download-retry.ps1` | 1/1 passed |
| Clean ARM Release | `cmake --fresh --preset Release`, `cmake --build --preset Release --target clean`, then `cmake --build --preset Release` | 35/35 Ninja steps passed |

The ARM compiler was `arm-none-eabi-gcc` GNU 13.3.1 from STM32CubeCLT. Final build output after the review fix reported RAM 2072 B/20 KB (10.12%) and Flash 10540 B/63 KB (16.34%).

## Protocol and configuration compatibility

| Area | Previous behavior | Task 3 behavior |
| --- | --- | --- |
| Spray mode | values 0..4 | values 0..4 unchanged; `SPRAY_CUSTOM=5` appended |
| Commanded mask | derived legacy modes only | holding 0x000C accepts one 8-bit value atomically and selects Custom |
| Capabilities | unavailable | 0x010A = 2; 0x010B = 0x0001 |
| Firmware version | 0x0201 | 0x0202, inside the discovery-compatible range |
| Actual output | 0x0108 | remains PWM-gated physical output/readback |
| Flash schema | v3 only | writes v4; decodes valid v3 and v4; record remains 24 bytes, CRC bytes 0..19 to 20..23 |
| Flash byte 19 | reserved | effective mask for every v4 mode; ignored for v3 |

Legacy masks are unchanged: AllOff 0x00, Group4 0xC0, Groups34 0xF0, Groups234 0xFC, and AllGroups 0xFF. Decode rejection restores the entire default configuration, including mask 0xFF.

## Self-review and concerns

- Reviewed scope against the task brief: no desktop source, CubeMX-generated code, toolchain definition, download script, or external baseline was changed.
- Register writes preserve prior save key, PWM-master, duty-bound, target-current, and batch semantics; 0x000C requires exactly one 8-bit value and leaves configuration unchanged on error.
- No hardware was flashed or physically exercised. Hardware acceptance, actual valve polarity/wiring confirmation, and desktop protocol-v2 client integration remain future work.
