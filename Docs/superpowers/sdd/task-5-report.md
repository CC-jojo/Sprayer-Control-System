# Task 5 Report: Atomic local configuration and interaction ViewModels

## Scope and result

Implemented only the Task 5 App services/ViewModels and deterministic tests. Protocol, firmware, serial/discovery behavior, WPF views/styles, and external baselines were not edited. The task started at `e869b30356350160cee429b5d514904317a8b067`; the untracked task brief was the expected pre-existing worktree item.

## TDD and verification evidence

| Phase | Command/result | Total |
| --- | --- | --- |
| RED | New focused tests compiled before product files; expected `CS0234` reported that `SprayerControl.App.ViewModels` and `IHostConfigurationStore` did not exist. | 0 executed; compile stopped as expected |
| GREEN | Focused Release tests after the minimal store/ViewModel implementation. | 19 passed, 0 failed |
| RED | Schema-cause regression required wrong-schema `InvalidDataException` to retain an inner cause. | 0 passed, 1 failed |
| RED | Controlled queued-mask and queued-setpoint tests exposed stale mask derivation (`0x02` instead of `0x03`) and mixed setpoint values (`46,350` instead of `46,360`). | 0 passed, 2 failed |
| GREEN | Final focused Release command. | 21 passed, 0 failed |
| Full | `dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release` | 163 passed, 0 failed |
| Build | `dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release` | 0 warnings, 0 errors |
| Diff | `git diff --check` | exit 0 |

## Changed files

- App services: `Services/IHostConfigurationStore.cs`, `Services/JsonHostConfigurationStore.cs`.
- App ViewModels: `ViewModels/ObservableObject.cs`, `AsyncCommand.cs`, `LogViewModel.cs`, `DashboardViewModel.cs`, `SettingsViewModel.cs`.
- Tests: `JsonHostConfigurationStoreTests.cs`, `DashboardViewModelTests.cs`, `SettingsViewModelTests.cs`, `LogViewModelTests.cs`.
- Durable records: `PROJECT_CONTEXT.md`, `Docs/DECISIONS.md`, `Docs/PROGRESS.md`, `Docs/HANDOFF.md`, this report, and the supplied Task 5 brief.

## Atomicity evidence

- Each store instance serializes saves with `SemaphoreSlim`.
- A save validates/clones schema-1 configuration, writes UTF-8 JSON to a unique same-directory temporary file, flushes it to disk, then uses `File.Replace` for an existing target or same-directory `File.Move` for a new target.
- The deterministic injected replacement-failure test proves the original bytes remain unchanged and the temporary file is removed. The cancellation test proves the same prior-file preservation before replacement. Concurrent saves leave one valid document.

## Dashboard state transitions

| Action | Before controller confirmation | On success | On failure/cancellation |
| --- | --- | --- | --- |
| Nozzle, side master, preset | Preview target; nozzle pending | One mask write, status readback becomes confirmed/preview mask | Preview returns to confirmed; error and full log entry |
| PWM enable | Setpoint pending | Controller response then status refresh | Error and full log entry |
| Duty/opening drag | Local preview only | No send | Not applicable |
| Duty/opening release | Setpoint pending | Gate snapshots duty/opening together, sends one mapped pair, then promotes that snapshot after status refresh; a later preview stays unconfirmed | Both previews return to confirmed; error and full log entry |

## Self-review and concerns

- Configuration accepts only schema 1 and preserves underlying parse/validation causes in `InvalidDataException`; it contains no credentials. Settings intentionally has no default calibration table.
- Controller output mask remains the dashboard nozzle authority. A static weak per-session gate prevents Dashboard ViewModels sharing one session from racing hardware operations.
- Controlled concurrent tests prove that queued nozzle actions derive from the latest confirmed mask inside the gate, and that setpoint write/promote values come from one in-gate snapshot rather than mutable previews.
- No serial controller, WPF UI automation, or spraying hardware was used. The atomic replacement and interaction transitions are covered with deterministic file/session fakes only.
