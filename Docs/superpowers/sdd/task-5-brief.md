# Task 5 Brief: Atomic local configuration and interaction ViewModels

## Baseline and scope

Start at approved HEAD `e869b30356350160cee429b5d514904317a8b067`. Read project rules/context, requirements/decisions/protocol, design spec, Task 5 plan, Task 2 domain types, Task 4 session services, and current tests before editing. Stop on unexpected tracked changes.

Editable product files:

- `Source/SprayerDesktop/SprayerControl.App/Services/IHostConfigurationStore.cs`
- `Source/SprayerDesktop/SprayerControl.App/Services/JsonHostConfigurationStore.cs`
- `Source/SprayerDesktop/SprayerControl.App/ViewModels/ObservableObject.cs`
- `Source/SprayerDesktop/SprayerControl.App/ViewModels/AsyncCommand.cs`
- `Source/SprayerDesktop/SprayerControl.App/ViewModels/DashboardViewModel.cs`
- `Source/SprayerDesktop/SprayerControl.App/ViewModels/SettingsViewModel.cs`
- `Source/SprayerDesktop/SprayerControl.App/ViewModels/LogViewModel.cs`

Tests/records:

- `Source/SprayerDesktop/SprayerControl.Tests/JsonHostConfigurationStoreTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/DashboardViewModelTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/SettingsViewModelTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/LogViewModelTests.cs`
- minimal existing test/project file changes only if required
- `PROJECT_CONTEXT.md`, `Docs/PROGRESS.md`, `Docs/DECISIONS.md`, `Docs/HANDOFF.md`
- this brief and `Docs/superpowers/sdd/task-5-report.md`

Do not edit protocol/firmware, WPF views/styles, external baselines, or existing serial/discovery behavior.

## Configuration store contract

- `IHostConfigurationStore` exposes async nullable load and validated save.
- Default file: `%LocalAppData%/SprayerTouchHMI/host-configuration.json`; constructor accepts an explicit path for tests.
- Current accepted schema is exactly `1`. Missing file returns `null`. Malformed JSON, null root, wrong schema, invalid bounds/points/direction/timestamp become a clear `InvalidDataException` with original cause preserved.
- Save validates/reconstructs the immutable `HostConfiguration`, writes UTF-8 JSON to a same-directory unique temp file, flushes to disk, then atomically replaces/moves the target. On any failure, delete the temp file and preserve the prior valid target byte-for-byte.
- Provide the smallest injectable file-operation seam needed to deterministically test replacement failure; no general filesystem framework.
- Never store credentials. Concurrent saves on one store instance must be serialized. Cancellation before replacement must not damage the prior file.
- Test valid/default-options round-trip, sorted points/UTC, missing, malformed/null/wrong schema/wrong point count/direction reversal, temp cleanup, replacement failure preservation, cancellation, and concurrent saves producing one valid final document.

## ViewModel primitives/logging

- `ObservableObject` correctly raises `INotifyPropertyChanged`; `AsyncCommand` implements `ICommand`, prevents overlapping execution per command, observes exceptions through an injectable/reporting path, and updates `CanExecute`.
- `LogViewModel` keeps the complete ordered log and exposes exactly the newest five as a read-only/rebindable recent view. Adding beyond five must not delete full history. Clear is explicit.

## Dashboard contract

Consume `IControllerSession`, `HostConfiguration?`, and `LogViewModel`. Expose at minimum:

- confirmed/preview mask, duty, return opening;
- `IsNozzleCommandPending`, `IsSetpointCommandPending`, connection/capability state, latest `ControllerStatus`, error text;
- commands/methods usable by later WPF for individual nozzle, left/right master, canopy preset, PWM master, refresh, preview-duty/opening, and commit-on-release.

Behavior:

- Initial/refreshed equipment state comes from `ReadStatusAsync`; actual `OutputMask` is authoritative.
- Mask operations derive from `NozzleMask`, serialize through one operation gate, set pending, send once, then read status and confirm from `0x0108`. Failure/cancellation rolls preview back to confirmed and records error/log; Legacy capability disables/rejects explicit-mask commands with a clear firmware message.
- Preview changes never send. Release/commit sends exactly once. Duty range uses host PWM minimum through fixed 80. Opening range uses safety minimum through 100.
- Setpoint commit maps preview opening through the ten-point calibration, rounds mA to microamps deterministically (midpoint away from zero), writes unified duty/current once, and only then promotes previews to confirmed. Failure rolls both back.
- Missing configuration/calibration locks setpoint commit without sending. Safety-minimum violations reject before sending.
- PWM command waits for controller response and refreshes authoritative status. Commands share a session-level operation gate so equipment writes cannot race.
- Keep full logs and five recent entries; no invented pressure/diagnostic state.

## Settings contract

- Edit PWM lower bound, return safety minimum, and exactly ten `10..100` calibration currents; expose validated curve points for later preview.
- Load an optional existing configuration into editable copies. Save builds a new validated schema-1 `HostConfiguration` with UTC timestamp and calls the store once.
- Invalid input/direction does not write and exposes a clear validation error. Successful save exposes the immutable saved configuration and clears errors.
- Do not add a default calibration table or STM32 persistence.

## TDD, report, commit

Add focused tests first and record real RED. Use deterministic fakes (no serial/UI/hardware). Then run focused tests, full Release solution tests, Release build, and `git diff --check`. Update durable docs with exact totals and current phase.

Report status, exact changed files, RED/GREEN/full/build totals, atomicity evidence, state-transition table, self-review, concerns. Commit exact message:

`feat: add local calibration store and touch interaction state`

Return concise status, HEAD, totals, report path, concerns.
