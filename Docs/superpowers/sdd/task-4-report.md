# Task 4 Report: Desktop protocol v2 client and controller session

## Scope and result

Implemented only the Task 4 desktop Core/App service boundary and its tests. Firmware, WPF views/code-behind, local persistence, and external baselines were not edited. The task began at `220982a5d8c6e5b033b025f640b8e388dbfd6973` with the expected untracked task brief as the only worktree change.

## TDD evidence

| Phase | Command/result | Total |
| --- | --- | --- |
| RED | Focused Release test command after test-first additions failed with expected `CS0234`: `SprayerControl.App.Services` did not exist. | 0 executed; compile stopped as expected |
| RED | Targeted typed-write-exception regression before its minimal validation-order repair. | 8 passed, 1 failed |
| RED | Review regression: owned lifetime cleanup during capability protocol/timeout/cancellation failure. | 0 passed, 3 failed |
| RED | Review regression: non-five-byte exception frames must not expose an exception code or fall back to Legacy. | 0 passed, 2 failed |
| RED | Review regression: second demo mutation entered its delay while the first was pending. | 1 passed, 1 failed |
| GREEN | Final focused Release command after all fixes. | 47 passed, 0 failed |
| Full | `dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release` | 142 passed, 0 failed |
| Build | `dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release` | 0 warnings, 0 errors |
| Diff | `git diff --check` | exit 0 |

The first GREEN run exposed a duplicate test-synchronization completion, corrected by using an idempotent `TrySetResult`; it was a test harness issue, not a product failure. The rerun passed 36/36, and the finalized focused suite passed 47/47.

## Exact changed-file inventory

- Documentation: `PROJECT_CONTEXT.md`; `Docs/DECISIONS.md`, `Docs/HANDOFF.md`, `Docs/PROGRESS.md`, `Docs/PROTOCOL.md`, `Docs/superpowers/sdd/task-4-brief.md`, `Docs/superpowers/sdd/task-4-report.md`.
- App/Core: `Source/SprayerDesktop/SprayerControl.App/SerialModbusChannel.cs`; `Services/IControllerSession.cs`, `Services/ModbusControllerSession.cs`, `Services/DemoControllerSession.cs`; `SprayerControl.Core/ControllerCapabilities.cs`, `ControllerClient.cs`, `ControllerSettings.cs`, `ModbusRtuCodec.cs`, `RegisterMap.cs`.
- Tests: `Source/SprayerDesktop/SprayerControl.Tests/SprayerControl.Tests.csproj`; `ControllerCapabilitiesTests.cs`, `ControllerClientTests.cs`, `ControllerSessionTests.cs`, `ControllerSettingsTests.cs`, `ModbusRtuCodecTests.cs`.

## Implemented behavior

| Area | Verified behavior |
| --- | --- |
| Registers/client | Adds 0x000C mask write, 0x010A/0x010B capability read, and one 0x0001..0x0009 nonpersistent setpoint batch with fixed zero placeholders and 30/80 bounds. |
| Compatibility/errors | `SprayMode.Custom=5`; immutable capabilities retain unknown bits; only a CRC-valid, exact five-byte typed exception `0x02` maps to legacy. CRC, slave/function, malformed, timeout, and other exception responses propagate. |
| Serial | Response header determines normal caller length versus an exact five-byte exception frame; partial frames still wait/timeout. |
| Sessions | Modbus session caches capability read once, gates legacy masks, delegates commands, and disposes an owned lifetime once, including on capability-read failure while preserving that failure. Demo session serializes mutations so the admitted operation owns delay/failure confirmation; queued cancellation cannot consume failure, and disposal prevents pending confirmation. |

## Self-review and concerns

- No hardware, serial port, or spraying output was used; serial exception framing is covered by the deterministic Core length helper rather than hardware I/O.
- Existing discovery, retry/reconnect, status parsing, PWM, save, and local persistence behavior were preserved. No second retry/reconnect policy was introduced.
- The test project now targets `net8.0-windows` and references the App project so the session contract is tested directly.
