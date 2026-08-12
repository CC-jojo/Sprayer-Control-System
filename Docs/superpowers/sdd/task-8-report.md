# Task 8 Report: integrated verification and paired release

## Source and package

Release `1.0.0` was packaged from source commit `be19ee618c9a80a9718147ff760b416972c05502` before the release commit. `Release/Desktop/` is a framework-dependent `win-x64` .NET 8 publish. `Release/Firmware/` contains the freshly compiler-built `SprayerController.elf`, `SprayerController.hex`, and `SprayerController.bin`. The paired protocol expectation is firmware `0x0202`, protocol version `2`, explicit-mask feature `0x0001`, and holding register `0x000C`.

## Verification

| Check | Result |
| --- | --- |
| Desktop Release tests | 170 passed, 0 failed, 0 skipped |
| Desktop publish | `win-x64`, framework-dependent, completed without warnings/errors |
| Native firmware CTest | 2/2 passed |
| Firmware PowerShell checks | 2/2 passed |
| ARM Release build | GNU Arm Embedded Toolchain 13.3.1; 35/35 Ninja steps; Flash 10540 B / 63 KB (16.34%); RAM 2072 B / 20 KB (10.12%) |
| ARM CTest | 0 tests registered; `ctest` exited successfully with `No tests were found` (the ARM preset is compiler/linker verification, while native behavior is covered by the 2 MSVC host tests) |
| UI capture | six exact-pixel demo PNGs regenerated and dimension-verified |
| Release hashes | all `SHA256SUMS.txt` entries verified |
| Credential scan | no credential-like material found in tracked source, documentation, or release text |

## Hardware limitation

No STM32 target was flashed, and no physical serial controller, touchscreen, nozzle output, return valve, PWM output, or spraying hardware was exercised. The package is a software and compiler verification baseline, not physical acceptance.
