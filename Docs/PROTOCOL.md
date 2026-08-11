# Protocol baseline and v2 direction

## Baseline presently copied

The copied baseline is Modbus RTU and reports firmware version 0x0201. The desktop accepts the 0x0201 through <0x0300 version range during discovery. The following addresses are observed in the copied source and are recorded here to prevent accidental changes:

| Holding register | Baseline meaning |
| --- | --- |
| 0x0000 | Legacy spray mode (AllOff, Group4, Groups34, Groups234, AllGroups) |
| 0x0001 | Linked PWM duty |
| 0x0002–0x0005 | Legacy group-duty placeholders; writes must be zero |
| 0x0006 / 0x0007 | Baseline duty limits, fixed at 30 / 80 |
| 0x0008 | Persist-as-power-on-default enable |
| 0x0009 | Current-module target in microamps, maximum 20000 |
| 0x000A | Save command, key 0xA55A when persistence is enabled |
| 0x000B | PWM master enable |
| 0x0100–0x0109 | Status: PWM tick, current-module state/target/readback/faults, parameter error, firmware version, PWM enable, output mask, communication stopped |

The output mask at status 0x0108 is an eight-bit actual-output readback. It is authoritative for rendering controller state.

## Protocol-v2 direction

v2 must add an explicit writable eight-bit commanded nozzle mask and detectable feature/version information. The register address, feature bit semantics, read/write atomicity and legacy fallback are **not allocated by this baseline task**; allocate them only together with desktop/firmware tests and an explicit reviewed decision.

The v2 desktop must send validated commands, await status readback, render pending/confirmed/failed transitions, preserve a custom mask, and continue to follow the existing timeout/reconnect path. Legacy canopy modes may remain inside firmware for compatibility, but are not a substitute for confirmed explicit-mask control.
