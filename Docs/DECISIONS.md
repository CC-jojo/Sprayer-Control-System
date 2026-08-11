# Durable decisions

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-08-12 | Create Source/SprayerDesktop and Source/SprayerController as verified copies, not links or in-place edits. | Both original projects are immutable comparison baselines. |
| 2026-08-12 | Exclude only generated directories named bin, obj, build, cmake-build-*, .git, and .vs during copying. | Retains all source, tests, CubeMX, tools, README, IDE and toolchain definitions needed to reproduce the baseline. |
| 2026-08-12 | Track SHA-256 manifests for included baseline files. | Provides a repeatable proof that copied source is exactly the pre-copy baseline and that copying did not alter it. |
| 2026-08-12 | Keep source and toolchain-sensitive paths English ASCII. | Avoids toolchain and migration path issues; explanatory documentation may remain Chinese. |
| 2026-08-12 | Keep the desktop on .NET 8 WPF and evolve protocol behavior incrementally behind tested layers. | The working Modbus, discovery and reconnect behavior is a valuable baseline; an unrelated framework rewrite is out of scope. |
| 2026-08-12 | Use an explicit eight-bit nozzle mask for protocol v2, with controller readback authoritative. | Individual nozzles, side masters, presets and custom patterns require one unambiguous confirmed state. |
| 2026-08-12 | Store calibration, PWM lower bound and safety minimum locally with atomic persistence. | The ten-point calibration is host configuration, not STM32 Flash data. |
| 2026-08-12 | Fix PWM upper limit at 80 percent pending thermal validation. | Hardware safety evidence is required before changing that limit. |
| 2026-08-12 | Allocate protocol-v2 holding register 0x000C and capabilities 0x010A..0x010B. | Explicit eight-nozzle commands and version/feature negotiation must be unambiguous while retaining legacy modes. |
| 2026-08-12 | Use Flash schema v4 while decoding schema v3. | The existing 24-byte record retains its layout and CRC coverage; reserved byte 19 now persists the effective nozzle mask. |
