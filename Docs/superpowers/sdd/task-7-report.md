# Task 7 Report: responsive sprayer touch views

## Result

Task 7 now delivers a native WPF touch HMI that follows the approved reference at 1920x1080 while retaining complete 16:9 and 16:10 layouts. The dashboard uses the reference's compact three-column composition, near-black navy background, deep blue-black cards, desaturated borders, white/blue-gray text, cyan gauges and selection, green connection/power/readback, and orange active nozzles/return safety.

The left card contains a pure-circle 4x2 nozzle array, canopy ladder with four presets, and two side masters. The center contains two 240-degree segmented horseshoe gauges with exact release values and mapped return-current display. The right contains live connection/error presentation, authoritative 4x2 nozzle readback, exactly five timestamped log rows, and full-log navigation.

Local calibration/validation status is distinct from communication failure. Missing calibration keeps the real session connected, disables both setpoint gauges, sends nothing, and shows `比例阀尚未完成标定，请先在设置中完成十点标定`; automatic reconnect is driven only by the explicit communication-fault flag.

Settings now shows the real ten-point curve and safety-minimum mapped current, requires a separate safety-minimum confirmation, blocks demo persistence, and exposes a separately confirmed real controller power-on-default sequence. Disabled states explain why the action is unavailable.

## TDD and verification

| Stage | Evidence |
| --- | --- |
| RED | New timestamp, demo-persistence, mapped-current, reference-layout, real-default, and visual-contract assertions failed before implementation. |
| Focused | Task 7 focused tests passed after implementation. |
| Full | `dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release`: 170 passed, 0 failed. |
| Build | `dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release`: 0 warnings, 0 errors. |
| Diff | `git diff --check`: exit 0. |
| Visual | Personally inspected 1920x1080, 1366x768, 1280x800 and 1920x1200: all sections present, no crop, overlap, white border or distorted nozzle circles. |

## Capture evidence

`Tools/Capture-UIReview.ps1` launches the app with a safe argument array. The app performs an off-screen exact-pixel `RenderTargetBitmap` render and exits nonzero if capture fails.

| PNG | SHA-256 |
| --- | --- |
| `dashboard-1280x800.png` | `C61AD46CE582B167F1807172316922E90A4DBBCEEA9977657C91B27DFAC8AB22` |
| `dashboard-1366x768.png` | `00C24B68B05B1910695A0940B19561C5FBB74C3266E4DAA1264F56C353502DB4` |
| `dashboard-1536x864.png` | `168373AC985689DBDE42F498778C1BA55ECDC6568D3392451B02AFD42AAED8D1` |
| `dashboard-1600x900.png` | `A51187BD2343AA10164D3AA2F50DFED5E510413F1B79E00D2C22F761067DC633` |
| `dashboard-1920x1080.png` | `3260BB401C75D7702C1C1D52AD04BC9B98059E97CE2E5B9F84CB73918BF99195` |
| `dashboard-1920x1200.png` | `4C23F13E44065FD3F4611B742DE8F312BFEAB7C9535B1FB4F47806687AF64F8E` |

## Known hardware limit

No physical controller, serial port, touchscreen, spraying outputs or STM32 target was available. Protocol behavior and UI state transitions are covered by automated tests and demo capture; physical hardware acceptance remains required before field deployment.
