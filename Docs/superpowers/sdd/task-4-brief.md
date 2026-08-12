# Task 4 Brief: Desktop protocol v2 client and controller session

## Context

Task 3 firmware protocol v2 is approved at `220982a5d8c6e5b033b025f640b8e388dbfd6973`. This task implements the matching desktop wire APIs and a testable application session boundary. It must preserve discovery, retry, reconnect, legacy settings, status parsing, and existing serial behavior.

## Read first

- `AGENTS.md`, `PROJECT_CONTEXT.md`, `Docs/REQUIREMENTS.md`, `Docs/DECISIONS.md`, `Docs/PROTOCOL.md`, `Docs/PROGRESS.md`
- `Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md`
- Task 4 in `Docs/superpowers/plans/2026-08-12-sprayer-touch-hmi.md`
- Existing `ControllerClient`, `ControllerDiscovery`, `ControllerSettings`, `RegisterMap`, `ModbusRtuCodec`, `ReliableModbusChannel`, `SerialModbusChannel`, and their tests

## Editable scope

Core/product:

- `Source/SprayerDesktop/SprayerControl.Core/RegisterMap.cs`
- `Source/SprayerDesktop/SprayerControl.Core/ControllerSettings.cs`
- `Source/SprayerDesktop/SprayerControl.Core/ControllerClient.cs`
- `Source/SprayerDesktop/SprayerControl.Core/ControllerCapabilities.cs`
- `Source/SprayerDesktop/SprayerControl.Core/ModbusRtuCodec.cs`
- `Source/SprayerDesktop/SprayerControl.App/SerialModbusChannel.cs`
- `Source/SprayerDesktop/SprayerControl.App/Services/IControllerSession.cs`
- `Source/SprayerDesktop/SprayerControl.App/Services/ModbusControllerSession.cs`
- `Source/SprayerDesktop/SprayerControl.App/Services/DemoControllerSession.cs`

Tests/project wiring/docs:

- `Source/SprayerDesktop/SprayerControl.Tests/SprayerControl.Tests.csproj`
- `Source/SprayerDesktop/SprayerControl.Tests/ControllerClientTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/ControllerCapabilitiesTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/ControllerSessionTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/ModbusRtuCodecTests.cs`
- `Docs/PROTOCOL.md`, `Docs/PROGRESS.md`, `Docs/DECISIONS.md`, `Docs/HANDOFF.md`, `PROJECT_CONTEXT.md`
- `Docs/superpowers/sdd/task-4-brief.md`, `Docs/superpowers/sdd/task-4-report.md`

Do not edit firmware, WPF views/code-behind, local persistence, external baselines, or unrelated files.

## Protocol contract

- Add constants: `NozzleMask=0x000C`, `CapabilityStart=0x010A`, `CapabilityCount=2`, explicit-mask feature `0x0001`; retain `StatusStart=0x0100`, `StatusCount=10`.
- Append `SprayMode.Custom=5` without renumbering values `0..4` and keep existing settings validation valid for the defined value.
- Define immutable `ControllerCapabilities(ushort ProtocolVersion, ushort FeatureFlags)` with `Legacy = new(1,0)` and `SupportsExplicitNozzleMask` based on bit `0x0001`. Preserve unknown feature bits.
- `ControllerClient.SetNozzleMaskAsync(NozzleMask)` writes one register at `0x000C` via function `0x06`, validates the echo, and honors cancellation.
- `ControllerClient.ReadCapabilitiesAsync()` reads two registers from `0x010A`. It returns parsed values on success. Only Modbus exception code `0x02` (illegal address) maps to `Legacy`; CRC, slave, function, malformed response, timeout, and other exception codes propagate.
- Extend `ModbusProtocolException` with a typed nullable exception code. Do not parse localized message text.
- Add a client API for unified duty/current setpoints that writes `0x0001..0x0009` atomically without changing mode/mask and validates duty `30..80`, current `0..20000`, fixed bounds `30/80`, zero group placeholders, and persistence off.
- Existing `WriteSettingsAsync`, PWM, save, and 10-register status parsing must keep their behavior.

## Real serial exception-frame handling

Legacy firmware returns a 5-byte Modbus exception frame to the capability read, while the normal response is 9 bytes. `SerialModbusChannel` must not wait for all 9 bytes after receiving a valid exception header.

- Read enough header bytes to distinguish a normal response from `function|0x80`; finish at 5 bytes for an exception and at the caller-supplied length otherwise.
- Keep timeout/cancellation/serialization semantics and do not accept partial frames.
- Factor the response-length decision into a deterministic testable Core helper if needed. Tests must prove normal/exception length decisions and typed illegal-address parsing. Do not require serial hardware.

## Session contract

Create `SprayerControl.App.Services.IControllerSession : IAsyncDisposable` with:

- `bool IsConnected`
- `ControllerCapabilities Capabilities`
- `Task SetNozzleMaskAsync(NozzleMask, CancellationToken)`
- `Task SetPwmEnabledAsync(bool, CancellationToken)`
- `Task WriteOperatingSetpointsAsync(ushort duty, ushort targetCurrentMicroamps, CancellationToken)`
- `Task<ControllerStatus> ReadStatusAsync(CancellationToken)`

`ModbusControllerSession`:

- Is created asynchronously from a `ControllerClient` plus optional owned `IAsyncDisposable`; reads/caches capabilities exactly once.
- Rejects explicit-mask commands on Legacy with a clear `NotSupportedException` mentioning firmware/protocol support; delegates supported operations to the client.
- Disposing marks disconnected and disposes the owned lifetime exactly once. Operations after disposal fail predictably.
- Do not create a second retry/reconnect policy; the existing channel/discovery layers remain responsible.

`DemoControllerSession`:

- Requires no serial hardware and defaults to protocol v2 explicit-mask capability.
- Maintains commanded mask separately from controller-authoritative actual status; actual `OutputMask` is commanded mask only when PWM is enabled, otherwise zero.
- Each mutating operation awaits an injectable delay, then confirms state; cancellation prevents confirmation.
- Provides a deterministic one-shot failure injection API for tests/demo. A failed operation must not mutate confirmed state; later operations can succeed.
- Validates the same duty/current ranges. Disposal is idempotent and later operations fail.

## TDD and verification

1. Add tests first and record real RED evidence before production changes.
2. Cover exact request bytes/registers/counts, capability success, illegal-address-only fallback, all other exception propagation, cancellation, setpoint batch payload, and no-send validation failures.
3. Add session tests for capability caching, Legacy gating, delegation, owned disposal, Demo delay/cancellation/failure/no-mutation, PWM-gated readback, setpoints, and post-dispose behavior.
4. If the test project must reference the App project, make the smallest Windows-targeting/project-reference change and keep all existing tests runnable.
5. Run focused tests and the full desktop Release solution; run `dotnet build` Release and `git diff --check`.

## Report and commit

- Start at exact HEAD `220982a5d8c6e5b033b025f640b8e388dbfd6973`; stop on unexpected tracked changes.
- Update durable protocol/progress/context/handoff records with verified behavior only.
- Write `Docs/superpowers/sdd/task-4-report.md` with RED/GREEN totals, changed files, compatibility/error table, serial exception behavior, session semantics, build output, self-review, and concerns.
- Commit exact message: `feat: add desktop protocol v2 controller session`.
- Return status; commit hash; changed files; exact test/build totals; report path; concerns.
