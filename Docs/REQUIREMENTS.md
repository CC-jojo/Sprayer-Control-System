# Approved requirements

## Scope and isolation

- This is an independent English/ASCII-source-path project rooted at this directory. The immutable desktop and firmware baselines remain outside Source/ and are never edited.
- The delivered system remains a Windows .NET 8 WPF application plus STM32F103C8T6 firmware. Existing serial discovery, Modbus RTU, retry, reconnect, watchdog, PWM, current-module and CRC behavior must be preserved unless a test-backed change explicitly replaces it.

## Touch HMI

- The primary interface is a dark native-WPF, three-column, landscape touchscreen HMI. It must respond correctly at 16:9 and 16:10, including 1920x1080, 1366x768, 1600x900, 1280x800 and 1920x1200 with Windows scaling.
- Use responsive Grid star sizing, bounded minimum sizes, uniform circular controls and no non-uniform whole-screen stretching. Main touch targets are at least 56x56 DIP; frequently used controls are at least 64x64 DIP.
- The dashboard shows eight nozzle rings (four groups, left and right) as pure status indicators that are never individual touch controls. Left/right side masters and a four-position vertical canopy-height slider apply the Group 4 / Groups 3+4 / Groups 2+3+4 / All-groups presets; a custom mask remains custom.
- Controller readback, not the most recent UI command, is the authoritative nozzle and PWM state. Commands show preview, pending, confirmed, or rollback/failure state and prevent conflicting duplicate writes.

## PWM and return valve

- Keep PWM master and unified duty behavior. The Settings page owns a configurable PWM lower bound; the upper bound is fixed at 80 percent pending hardware thermal approval.
- Return-valve calibration is host-local and consists of exactly ten current points for 10 through 100 percent. Mapping between points is piecewise-linear.
- Settings owns a user-configurable return-valve safety minimum. The dashboard cannot select values below it; raising the minimum moves an existing lower setpoint smoothly and requires confirmation plus an operation-log entry.
- There is no special 0-percent hardware signal-cut feature. Firmware receives mapped current only. Missing, malformed, non-finite, out-of-range, or non-monotonic calibration blocks normal return-valve adjustment.
- Local calibration persists atomically with schema version and last-modified time. It contains no credentials or machine secret.

## Protocol, safety, and release

- Protocol v2 is the direction: it will expose an explicit eight-bit commanded nozzle mask and feature/version detection while retaining safe compatibility behavior.
- Firmware status must continue to report actual output mask. Flash does not contain the host calibration table.
- Settings contains only confirmed functionality: PWM lower bound, safety minimum, ten-point calibration, curve preview, local save, and clearly separated confirmed save-as-power-on-default behavior.
- A release pairs desktop and firmware versions with checksums. Hardware flashing and real-controller acceptance remain separate from this baseline task.

## Release acceptance

- Release `1.0.0` is a paired package: `Release/Desktop/` is a `win-x64`, framework-dependent publish and `Release/Firmware/` contains only the `.elf`, `.hex`, and `.bin` produced by its fresh ARM Release build.
- The pair requires firmware `0x0202` with protocol version `2` and explicit-nozzle feature bit `0x0001`; the desktop retains the documented illegal-address-only legacy fallback but the packaged pair is protocol v2.
- `Release/SHA256SUMS.txt` must cover every package file except itself using ASCII SHA-256 entries and forward-slash project-relative paths. Each listed hash must verify before handoff.
- Automated demo acceptance and exact-pixel review are release evidence only. Hardware flashing, serial-controller, physical-touch and spraying-output acceptance require separate authorized on-site validation and must never be inferred from this package.
