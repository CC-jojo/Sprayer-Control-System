# Protocol v2: explicit eight-nozzle mask

## Compatibility baseline

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

The output mask at status 0x0108 is an eight-bit actual PWM-gated output readback. It is authoritative for rendering controller state; PWM disable forces it to zero even when a command mask is retained. Firmware now reports 0x0202, which remains within the desktop discovery-compatible 0x0201 through <0x0300 range.

## Command and legacy-mode compatibility

Bit mapping is group 1 left/right = bits 0/1, group 2 = bits 2/3, group 3 = bits 4/5, and group 4 = bits 6/7. Even bits command left nozzles; odd bits command right nozzles.

| Register | Protocol-v2 allocation |
| --- | --- |
| 0x000C | Explicit commanded nozzle mask, one 8-bit value only |
| 0x010A | Protocol version 2 |
| 0x010B | Feature flags 0x0001: explicit nozzle mask |

| Mode value | Mode | Effective mask | v4 Flash byte 19 |
| --- | --- | --- | --- |
| 0 | AllOff | 0x00 | 0x00 |
| 1 | Group4 | 0xC0 | 0xC0 |
| 2 | Groups34 | 0xF0 | 0xF0 |
| 3 | Groups234 | 0xFC | 0xFC |
| 4 | AllGroups | 0xFF | 0xFF |
| 5 | Custom | exact 8-bit command | exact 8-bit command |

Holding register 0x000C accepts exactly one 0x0000..0x00FF value, selects Custom atomically, and leaves configuration unchanged on error. Registers 0x010A and 0x010B return protocol version 2 and feature flags 0x0001. Writing legacy mode 0x0000 restores its derived mask; writing Custom through 0x0000 retains the current mask.

## Flash-record compatibility

The record remains 24 bytes and half-word aligned, with the existing header and payload-size field. CRC32 covers bytes 0..19 and is stored at bytes 20..23.

| Schema | Decode behavior | Byte 19 |
| --- | --- | --- |
| 3 | Legacy modes only; derive mask from mode after CRC validation | Ignored |
| 4 | Legacy modes require their derived masks; Custom accepts any 8-bit value | Effective mask |

Header, schema, payload size, CRC, persistence byte, or configuration validation failures restore complete defaults: AllGroups, mask 0xFF, duty 30, bounds 30..80, persistence off, target current 0.
