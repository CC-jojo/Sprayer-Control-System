# Task 2 Report: Desktop domain model for nozzle masks and return-valve calibration

## Status

DONE

## Commit references

- Start: `0ad19bd18559708d77b8fb415abdfb7d276bd7fb`
- End: `HEAD` (canonical self-reference; resolve with `git rev-parse HEAD` after this report is committed)

## Changed files

- `Source/SprayerDesktop/SprayerControl.Core/NozzleMask.cs`
- `Source/SprayerDesktop/SprayerControl.Core/ReturnValveCalibration.cs`
- `Source/SprayerDesktop/SprayerControl.Core/HostConfiguration.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/NozzleMaskTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/ReturnValveCalibrationTests.cs`
- `Source/SprayerDesktop/SprayerControl.Tests/HostConfigurationTests.cs`
- `Docs/superpowers/sdd/task-2-brief.md`
- `Docs/superpowers/sdd/task-2-report.md`

## RED evidence

Before production source was added, the following focused test command exited `1` because the required domain API did not yet exist:

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.Tests\SprayerControl.Tests.csproj --configuration Release --filter "FullyQualifiedName~NozzleMaskTests|FullyQualifiedName~ReturnValveCalibrationTests|FullyQualifiedName~HostConfigurationTests"
```

Summary: `CS0246` / `CS0103` reported missing `CanopyPreset` and `CalibrationPoint` symbols in the newly added tests. This is the expected missing-production-behavior RED state.

An additional boundary RED was then recorded with:

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.Tests\SprayerControl.Tests.csproj --configuration Release --filter "FullyQualifiedName~ReturnValveCalibrationTests.SafetyAwareLookupRejectsInvalidSafetyMinimumAndOpeningBelowIt"
```

Summary: `1` failed, `0` passed. A non-finite requested opening produced `InvalidOperationException`; the test required the same `ArgumentOutOfRangeException` validation used by the ordinary interpolation overload. The safety-aware overload was adjusted to validate the requested opening before applying the safety-floor rule.

Review-regression RED was recorded with the focused command above after adding JSON, null-element, and UTC-normalization tests. Summary: `3` failed, `68` passed, `0` skipped. Default `System.Text.Json` deserialization could not bind the `IEnumerable<CalibrationPoint>` constructor parameter to `Points`; a null calibration element caused `NullReferenceException` during sorting; and an `+08:00` timestamp was retained instead of normalized to UTC. The constructor now accepts `IReadOnlyList<CalibrationPoint>` to match `Points`, snapshots and validates null elements before sorting, and stores non-default timestamps as UTC.

## GREEN and full-suite evidence

Focused GREEN command:

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.Tests\SprayerControl.Tests.csproj --configuration Release --filter "FullyQualifiedName~NozzleMaskTests|FullyQualifiedName~ReturnValveCalibrationTests|FullyQualifiedName~HostConfigurationTests"
```

Summary: `0` failed, `71` passed, `0` skipped.

Full desktop Release command:

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
```

Summary: `0` failed, `109` passed, `0` skipped.

## Behavior verification

| Area | Verified behavior |
| --- | --- |
| Nozzle mask | Fixed one-based group-to-bit mapping, individual changes, invalid-group and invalid-preset rejection, side-master preservation, value equality, and all four presets across four left/right flag combinations. |
| Calibration | Immutable sorted ten-point snapshot, controlled null-element rejection, exact openings, inclusive `0..20 mA` endpoints, monotonic increasing/decreasing/flat curves, interpolation from the `10%` lower boundary, and ordinary/safety-aware domain validation. |
| Host configuration | Immutable calibration clone, schema/PWM/safety/timestamp validation, UTC normalization, default `System.Text.Json` round-trip, and use of the calibration constructor's complete invariants. |

## Self-review

- `git diff --check` completed without whitespace errors.
- New production types are pure desktop-domain code; no Modbus, WPF, firmware, protocol-client, local-I/O, or external-baseline files changed.
- Public collections use a read-only snapshot, and host configuration clones the validated calibration rather than retaining caller-owned state.
- Finite-number checks precede interpolation; numeric operations do not parse or format culture-sensitive strings.
- `ReturnValveCalibration` constructor binding uses the same `IReadOnlyList<CalibrationPoint>` type exposed by `Points`, so default .NET 8 `System.Text.Json` can round-trip the host configuration without a persistence-layer converter.

## Concerns

None. The repository-local Git identity reuses the reviewed baseline author, `Codex <codex@local.invalid>`. The initial RED is a compile-time missing-API failure because this task creates all three production domain files from scratch; the focused and full Release suites subsequently passed.
