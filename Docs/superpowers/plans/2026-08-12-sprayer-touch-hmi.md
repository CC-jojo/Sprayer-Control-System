# Sprayer Touch HMI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver an independent, versioned Windows touchscreen HMI and matching STM32 firmware with eight-channel nozzle control, responsive UI, ten-point return-valve calibration, safety limits, tests, release artifacts, and durable migration context.

**Architecture:** Copy and verify the existing desktop and firmware baselines into the new English-ASCII project, then establish a local Git baseline. Extend the protocol with an explicit eight-bit nozzle mask and capability registers while preserving legacy behaviors; build a WPF MVVM interaction layer over the proven serial/Modbus core, with controller readback as the source of truth and local atomic storage for calibration.

**Tech Stack:** Windows PowerShell, Git, .NET 8, WPF/XAML, xUnit, C11, CMake, STM32 HAL/CMSIS, Modbus RTU

## Global Constraints

- Work only under `E:/CodexFILE/SprayerTouchHMI_2026-08-12`.
- Treat `E:/CodexFILE/Sprayer controller system/SprayerDesktop` and `E:/CodexFILE/Sprayer controller system/SprayerController` as immutable baselines.
- Keep all toolchain-sensitive project and source paths English ASCII.
- Preserve `.NET 8` WPF and the STM32F103C8T6 `Core`/`App`/`Platform` firmware architecture.
- Preserve proven serial discovery, Modbus RTU, retry, reconnect, PWM, current-module, watchdog, and CRC behavior unless a test-backed protocol extension requires a scoped change.
- Render equipment status from controller readback, not optimistic UI state.
- Support 16:9 and 16:10 landscape touchscreens at `1366×768`, `1600×900`, `1920×1080`, `1280×800`, and `1920×1200`.
- Implement eight independently controllable nozzles, left/right masters, and canopy presets.
- Use ten return-valve points at `10% ... 100%`, piecewise-linear interpolation, a configurable safety minimum, and no special `0%` signal-cut function.
- Store calibration and UI safety limits locally on the host; do not store the ten-point table in STM32 Flash.
- Keep PWM upper duty fixed at `80%`; its lower bound is host-configurable within the firmware-valid range.
- Do not add unapproved pressure, field-test, diagnostics, restore, branding, or unrelated settings UI.
- Do not claim a fresh STM32 build when no working compiler ran it.
- Never place credentials, account tokens, or private keys in the project.

---

### Task 1: Verified source copies and reproducible project baseline

**Files:**
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Tools/Initialize-SourceCopies.ps1`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/BASELINE_MANIFEST_DESKTOP.sha256`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/BASELINE_MANIFEST_FIRMWARE.sha256`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/REQUIREMENTS.md`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/DECISIONS.md`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/PROGRESS.md`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/PROTOCOL.md`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Docs/HANDOFF.md`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Source/SprayerDesktop/**`
- Create: `E:/CodexFILE/SprayerTouchHMI_2026-08-12/Source/SprayerController/**`

**Interfaces:**
- Consumes: the two immutable baseline directories.
- Produces: clean source copies, sorted SHA-256 manifests, a local Git baseline commit, and migration documentation.

- [ ] **Step 1: Write the guarded copy script**

  Implement a PowerShell script with parameters `DesktopSource`, `FirmwareSource`, and `ProjectRoot`. It must reject a pre-populated `Source/SprayerDesktop` or `Source/SprayerController`, exclude generated directories (`bin`, `obj`, `build`, `cmake-build-*`, `.git`), copy with `robocopy`, generate manifests using relative paths, and compare source/copy manifests byte-for-byte.

```powershell
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DesktopSource,
    [Parameter(Mandatory)][string]$FirmwareSource,
    [Parameter(Mandatory)][string]$ProjectRoot
)
$ErrorActionPreference = 'Stop'
$targets = @{
    Desktop = Join-Path $ProjectRoot 'Source\SprayerDesktop'
    Firmware = Join-Path $ProjectRoot 'Source\SprayerController'
}
foreach ($target in $targets.Values) {
    if (Test-Path -LiteralPath $target) { throw "Refusing to overwrite $target" }
}
# Use robocopy /E with explicit generated-directory exclusions; accept exit codes 0..7.
# Enumerate included files with Get-ChildItem, hash with Get-FileHash SHA256,
# sort by forward-slash relative path, and compare source/copy manifest lines.
```

- [ ] **Step 2: Run the guarded copy and manifest verification**

  Run the script against the exact baseline paths. Expected: both copy manifests match their baseline manifests and neither baseline timestamp/hash changes.

```powershell
& .\Tools\Initialize-SourceCopies.ps1 `
  -DesktopSource 'E:\CodexFILE\Sprayer controller system\SprayerDesktop' `
  -FirmwareSource 'E:\CodexFILE\Sprayer controller system\SprayerController' `
  -ProjectRoot 'E:\CodexFILE\SprayerTouchHMI_2026-08-12'
```

- [ ] **Step 3: Record baseline tests**

  Run desktop tests before behavior changes.

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
```

  Expected: all existing xUnit tests pass. Run existing firmware PowerShell tool tests and record exact results. Probe CMake/compiler availability; do not treat missing compiler as a pass.

- [ ] **Step 4: Populate durable context documents**

  Copy confirmed requirements verbatim into `REQUIREMENTS.md`; record the isolation, explicit-mask protocol direction, ten-point calibration, safe lower bound, local storage, responsive touch UI, and no-0%-cut decisions in `DECISIONS.md`. Create an initial protocol table and handoff checklist. Record commands and outputs in `PROGRESS.md`.

- [ ] **Step 5: Initialize local Git and commit the verified baseline**

```powershell
git init
git add AGENTS.md PROJECT_CONTEXT.md Docs Source Tools
git commit -m "chore: establish verified independent sprayer project baseline"
```

  Expected: clean worktree and no configured remote.

---

### Task 2: Desktop domain model for eight nozzles and calibration

**Files:**
- Create: `Source/SprayerDesktop/SprayerControl.Core/NozzleMask.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Core/ReturnValveCalibration.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Core/HostConfiguration.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Tests/NozzleMaskTests.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Tests/ReturnValveCalibrationTests.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Tests/HostConfigurationTests.cs`

**Interfaces:**
- Produces: `NozzleMask`, `CanopyPreset`, `CalibrationPoint`, `ReturnValveCalibration`, and `HostConfiguration` used by protocol and UI tasks.

- [ ] **Step 1: Add failing mask tests**

```csharp
[Theory]
[InlineData(CanopyPreset.Group4, 0xC0)]
[InlineData(CanopyPreset.Groups34, 0xF0)]
[InlineData(CanopyPreset.Groups234, 0xFC)]
[InlineData(CanopyPreset.AllGroups, 0xFF)]
public void PresetCreatesExpectedEightBitMask(CanopyPreset preset, byte expected) =>
    Assert.Equal(expected, NozzleMask.ForPreset(preset, leftEnabled: true, rightEnabled: true).Value);

[Fact]
public void SideMastersUseEvenBitsForLeftAndOddBitsForRight()
{
    Assert.Equal(0x55, NozzleMask.All.WithRight(false).Value);
    Assert.Equal(0xAA, NozzleMask.All.WithLeft(false).Value);
}
```

- [ ] **Step 2: Implement the exact mask model**

```csharp
public enum CanopyPreset { Group4, Groups34, Groups234, AllGroups }
public readonly record struct NozzleMask(byte Value)
{
    public static NozzleMask None => new(0x00);
    public static NozzleMask All => new(0xFF);
    public bool IsEnabled(int group, bool right);
    public NozzleMask WithNozzle(int group, bool right, bool enabled);
    public NozzleMask WithLeft(bool enabled);
    public NozzleMask WithRight(bool enabled);
    public static NozzleMask ForPreset(CanopyPreset preset, bool leftEnabled, bool rightEnabled);
}
```

  Bit mapping is fixed: group 1 left/right = bits 0/1, group 2 = 2/3, group 3 = 4/5, group 4 = 6/7.

- [ ] **Step 3: Add failing ten-point calibration tests**

  Cover exactly ten points, missing/duplicate openings, non-finite/negative currents, interpolation at 35%, exact-point lookup, and safety-minimum enforcement.

```csharp
Assert.Equal(0.215, calibration.CurrentMilliampsAt(35), precision: 6);
Assert.Throws<InvalidOperationException>(() => calibration.CurrentMilliampsAt(20, safetyMinimumOpening: 30));
```

- [ ] **Step 4: Implement immutable calibration and host configuration**

```csharp
public sealed record CalibrationPoint(int OpeningPercent, double CurrentMilliamps);
public sealed class ReturnValveCalibration
{
    public IReadOnlyList<CalibrationPoint> Points { get; }
    public ReturnValveCalibration(IEnumerable<CalibrationPoint> points);
    public double CurrentMilliampsAt(double openingPercent);
}
public sealed record HostConfiguration(
    int SchemaVersion,
    int PwmDutyMinimum,
    int ReturnValveSafetyMinimum,
    IReadOnlyList<CalibrationPoint> ReturnValveCalibration,
    DateTimeOffset LastModifiedUtc);
```

  Require openings `10..100` in steps of 10, PWM minimum `30..80`, return safety minimum `10..100`, current values `0..20 mA`, and monotonic calibration in either one consistent direction. Reject direction reversals.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.Tests\SprayerControl.Tests.csproj --configuration Release
git add Source/SprayerDesktop
git commit -m "feat: add nozzle mask and return-valve calibration domain"
```

---

### Task 3: Firmware protocol v2 and explicit nozzle mask

**Files:**
- Modify: `Source/SprayerController/App/host_registers.h`
- Modify: `Source/SprayerController/App/host_registers.c`
- Modify: `Source/SprayerController/App/app_runtime.c`
- Modify: `Source/SprayerController/App/config_store.c`
- Modify: `Source/SprayerController/App/config_store.h`
- Modify: `Source/SprayerController/Platform/app_hal.c`
- Create: `Source/SprayerController/Tests/CMakeLists.txt`
- Create: `Source/SprayerController/Tests/test_host_registers.c`
- Create: `Source/SprayerController/Tests/test_config_store.c`

**Interfaces:**
- Consumes: bit mapping from Task 2.
- Produces: protocol v2 register `0x000C` for explicit mask, capability registers `0x010A..0x010B`, backward-compatible preset modes, and status readback.

- [ ] **Step 1: Write host C tests before firmware changes**

```c
HostRegisters r;
HostRegisters_Init(&r);
uint16_t mask = 0x00A5U;
assert(HostRegisters_WriteBatch(&r, 0x000CU, &mask, 1U) == MB_OK);
assert(HostRegisters_GetOutputMask(&r) == 0xA5U);
assert(r.config.mode == SPRAY_CUSTOM);
assert(HostRegisters_WriteBatch(&r, 0x000CU, (uint16_t[]){0x0100U}, 1U) == MB_ILLEGAL_VALUE);
```

  Add capability assertions: protocol version `2`, feature bit `0x0001` = explicit nozzle mask. Add legacy preset assertions for `0xC0`, `0xF0`, `0xFC`, `0xFF`.

- [ ] **Step 2: Extend the configuration and register model**

```c
typedef enum {
    SPRAY_ALL_OFF=0, SPRAY_GROUP4=1, SPRAY_GROUP34=2,
    SPRAY_GROUP234=3, SPRAY_ALL=4, SPRAY_CUSTOM=5
} SprayMode;

typedef struct {
    SprayMode mode;
    uint8_t nozzle_mask;
    uint8_t duty;
    uint8_t duty_min;
    uint8_t duty_max;
    bool persistence_enabled;
    uint16_t target_current_ua;
} ControllerConfig;
```

  `0x000C` accepts only `0x0000..0x00FF`, stores the byte, and selects `SPRAY_CUSTOM`. `HostRegisters_GetOutputMask` returns `nozzle_mask` for custom mode and the existing masks for legacy modes. `0x010A` returns `2`; `0x010B` returns `0x0001`.

- [ ] **Step 3: Version the Flash record compatibly**

  Bump the record schema while keeping decode support for the existing 24-byte record. A legacy decoded preset derives its mask from the preset. A v2 record persists `nozzle_mask`; CRC remains mandatory. Update record size only if required by the encoded layout and keep half-word alignment.

- [ ] **Step 4: Wire runtime and correct hardware mapping documentation**

  Ensure `AppRuntime` applies the explicit mask without the old `mode != SPRAY_ALL_OFF` guard suppressing a valid custom mask. Correct the `WriteValveOutputs` comment to document even-bit left and odd-bit right mapping.

- [ ] **Step 5: Run firmware host tests and available builds**

```powershell
cmake -S .\Source\SprayerController\Tests -B .\Temp\firmware-host-tests
cmake --build .\Temp\firmware-host-tests --config Release
ctest --test-dir .\Temp\firmware-host-tests -C Release --output-on-failure
```

  Also run existing PowerShell tool tests and the STM32 CMake build when its compiler is available. Record missing-compiler evidence if unavailable.

- [ ] **Step 6: Update protocol document and commit**

```powershell
git add Source/SprayerController Docs/PROTOCOL.md Docs/PROGRESS.md
git commit -m "feat: add protocol v2 explicit eight-nozzle mask"
```

---

### Task 4: Desktop protocol v2 client and controller-session abstraction

**Files:**
- Modify: `Source/SprayerDesktop/SprayerControl.Core/RegisterMap.cs`
- Modify: `Source/SprayerDesktop/SprayerControl.Core/ControllerSettings.cs`
- Modify: `Source/SprayerDesktop/SprayerControl.Core/ControllerClient.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Core/ControllerCapabilities.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Services/IControllerSession.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Services/ModbusControllerSession.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Services/DemoControllerSession.cs`
- Modify: `Source/SprayerDesktop/SprayerControl.Tests/ControllerClientTests.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Tests/ControllerCapabilitiesTests.cs`

**Interfaces:**
- Produces: capability detection, explicit-mask write, status stream, and a fakeable application-facing session.

- [ ] **Step 1: Add failing protocol request/response tests**

  Assert `SetNozzleMaskAsync(new NozzleMask(0xA5))` writes register `0x000C`; assert capability reads `0x010A` count 2; assert a legacy illegal-address capability response produces `ControllerCapabilities.Legacy` rather than breaking connection.

- [ ] **Step 2: Implement protocol constants and capability model**

```csharp
public const ushort NozzleMask = 0x000C;
public const ushort CapabilityStart = 0x010A;
public const ushort CapabilityCount = 2;

public sealed record ControllerCapabilities(ushort ProtocolVersion, ushort FeatureFlags)
{
    public bool SupportsExplicitNozzleMask => (FeatureFlags & 0x0001) != 0;
    public static ControllerCapabilities Legacy => new(1, 0);
}
```

- [ ] **Step 3: Add explicit-mask and capability APIs**

```csharp
public Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default);
public Task<ControllerCapabilities> ReadCapabilitiesAsync(CancellationToken cancellationToken = default);
```

  New UI operation must be disabled with a clear firmware-version message when explicit mask support is absent.

- [ ] **Step 4: Introduce the session interface and demo implementation**

```csharp
public interface IControllerSession : IAsyncDisposable
{
    bool IsConnected { get; }
    ControllerCapabilities Capabilities { get; }
    Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken token = default);
    Task SetPwmEnabledAsync(bool enabled, CancellationToken token = default);
    Task WriteOperatingSetpointsAsync(ushort duty, ushort targetCurrentMicroamps, CancellationToken token = default);
    Task<ControllerStatus> ReadStatusAsync(CancellationToken token = default);
}
```

  Demo session must simulate pending delay, confirmation, and injectable failure without serial hardware.

- [ ] **Step 5: Run desktop tests and commit**

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
git add Source/SprayerDesktop Docs/PROTOCOL.md
git commit -m "feat: add desktop protocol v2 controller session"
```

---

### Task 5: Local configuration store and interaction ViewModels

**Files:**
- Create: `Source/SprayerDesktop/SprayerControl.App/Services/IHostConfigurationStore.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Services/JsonHostConfigurationStore.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/ViewModels/ObservableObject.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/ViewModels/AsyncCommand.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/ViewModels/DashboardViewModel.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/ViewModels/SettingsViewModel.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/ViewModels/LogViewModel.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Tests/JsonHostConfigurationStoreTests.cs`
- Create: `Source/SprayerDesktop/SprayerControl.Tests/DashboardViewModelTests.cs`

**Interfaces:**
- Consumes: Tasks 2 and 4.
- Produces: validated atomic local configuration and pending/confirmed/rollback UI state.

- [ ] **Step 1: Add failing atomic-store tests**

  Test valid round-trip, missing file, malformed JSON, wrong schema, wrong point count, direction reversal, temporary-file cleanup, and preservation of the previous valid file when replacement fails.

- [ ] **Step 2: Implement JSON store**

```csharp
public interface IHostConfigurationStore
{
    Task<HostConfiguration?> LoadAsync(CancellationToken token = default);
    Task SaveAsync(HostConfiguration configuration, CancellationToken token = default);
}
```

  Save to `<path>.tmp`, flush, then atomically replace/move. Default path is `%LocalAppData%/SprayerTouchHMI/host-configuration.json`; allow an injected path for tests.

- [ ] **Step 3: Add failing interaction-state tests**

  Cover individual nozzle command, side master, preset, custom mask, drag preview/no send, release/one send, pending state, confirmed status update, failed rollback, safety-minimum rejection, and missing-calibration lockout.

- [ ] **Step 4: Implement ViewModels with controller-confirmed state**

  `DashboardViewModel` exposes `ConfirmedMask`, `PreviewMask`, `IsNozzleCommandPending`, `ConfirmedDuty`, `PreviewDuty`, `ConfirmedReturnOpening`, `PreviewReturnOpening`, connection status, current status, and five recent logs. Commands serialize writes with `SemaphoreSlim` and restore confirmed values on failure.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
git add Source/SprayerDesktop
git commit -m "feat: add local calibration store and touch interaction state"
```

---

### Task 6: Responsive WPF design system and reusable touch controls

**Files:**
- Modify: `Source/SprayerDesktop/SprayerControl.App/App.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Themes/Colors.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Themes/Controls.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Controls/NozzleArrayControl.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Controls/NozzleArrayControl.xaml.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Controls/SegmentedGaugeControl.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Controls/SegmentedGaugeControl.xaml.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Controls/TouchNumericKeypad.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Controls/TouchNumericKeypad.xaml.cs`
- Modify: `Source/SprayerDesktop/SprayerControl.Tests/MainWindowLayoutTests.cs`

**Interfaces:**
- Produces: accessible styles and reusable controls bound by dependency properties/commands.

- [ ] **Step 1: Add XAML invariant tests**

  Assert full-bleed dark background, star-sized three-column grid, no whole-window non-uniform stretch, touch minimums `56`, settings numeric keypad, no main-screen `TextBox`, and required Chinese labels.

- [ ] **Step 2: Implement design tokens**

  Define named brushes for background/panel/border/text/muted/cyan/green/amber/red and styles for touch buttons, status buttons, cards, headings, and focus/disabled/pending states. Use dynamic resources rather than inline duplicate colors.

- [ ] **Step 3: Implement nozzle array control**

  Eight command-bound circular controls in a 2×4 grid, even spacing, group labels, confirmed/preview/pending states, and aspect-preserving `Viewbox Stretch="Uniform"` only around each visual symbol.

- [ ] **Step 4: Implement segmented gauge and numeric keypad**

  Gauge exposes `Minimum`, `Maximum`, `Value`, `PreviewValue`, `SafetyMinimum`, `IsPending`, and release command. Numeric keypad accepts digits, decimal separator, backspace, clear, cancel, and confirm; it never invokes the physical keyboard.

- [ ] **Step 5: Build and commit**

```powershell
dotnet build .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
dotnet test .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
git add Source/SprayerDesktop
git commit -m "feat: add responsive WPF touch design system"
```

---

### Task 7: Dashboard, settings, and full log views

**Files:**
- Replace: `Source/SprayerDesktop/SprayerControl.App/MainWindow.xaml`
- Modify: `Source/SprayerDesktop/SprayerControl.App/MainWindow.xaml.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Views/DashboardView.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Views/DashboardView.xaml.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Views/SettingsView.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Views/SettingsView.xaml.cs`
- Create: `Source/SprayerDesktop/SprayerControl.App/Views/LogView.xaml`
- Create: `Source/SprayerDesktop/SprayerControl.App/Views/LogView.xaml.cs`
- Modify: `Source/SprayerDesktop/SprayerControl.Tests/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: Tasks 5 and 6.
- Produces: functional main/settings/log touch screens matching the approved reference.

- [ ] **Step 1: Implement application shell and navigation**

  `MainWindow` hosts one current view, top title/connection/settings region, fullscreen behavior, and responsive minimum bounds. It constructs production services by default and permits demo-session injection for smoke testing.

- [ ] **Step 2: Implement the three-column dashboard**

  Left column: native eight-nozzle diagram, canopy selector, side masters. Center: PWM duty gauge/master and return-opening gauge with amber safety minimum. Right: connection card, 4×2 readback states, five logs, and full-log navigation. Preserve exact approved Chinese labels and full-bleed dark background.

- [ ] **Step 3: Implement minimal settings view**

  Cards: PWM lower bound, safety minimum, ten calibration cells, curve preview, local save, and separately confirmed existing save-as-power-on-default action. No unapproved categories. Calibration editing opens `TouchNumericKeypad`.

- [ ] **Step 4: Implement full log view**

  Show timestamp/message list, return button, and clear-log confirmation. Do not add export/filter functionality in this scope.

- [ ] **Step 5: Verify responsive layout and commit**

  Run automated XAML tests, then launch demo mode at all required resolutions and record screenshots under `Temp/UIReview`. Verify circles remain round, no white borders, no clipped Chinese text, and touch targets remain usable.

```powershell
git add Source/SprayerDesktop Temp/UIReview Docs/PROGRESS.md
git commit -m "feat: build responsive sprayer touchscreen views"
```

---

### Task 8: Integrated behavior, documentation, and paired release

**Files:**
- Modify: `PROJECT_CONTEXT.md`
- Modify: `Docs/REQUIREMENTS.md`
- Modify: `Docs/DECISIONS.md`
- Modify: `Docs/PROGRESS.md`
- Modify: `Docs/PROTOCOL.md`
- Modify: `Docs/HANDOFF.md`
- Create: `Release/VERSION.txt`
- Create: `Release/SHA256SUMS.txt`
- Create: `Release/Desktop/**`
- Create: `Release/Firmware/**`

**Interfaces:**
- Consumes: all prior tasks.
- Produces: verified paired desktop/firmware release and complete migration context.

- [ ] **Step 1: Run full desktop verification**

```powershell
dotnet test .\Source\SprayerDesktop\SprayerControl.sln --configuration Release
dotnet publish .\Source\SprayerDesktop\SprayerControl.App\SprayerControl.App.csproj `
  --configuration Release --runtime win-x64 --self-contained false `
  --output .\Release\Desktop
```

- [ ] **Step 2: Run firmware verification and collect artifacts**

  Run host C tests, tool PowerShell tests, and a fresh STM32 Release build if the compiler is available. Copy only verified `.elf`, `.hex`, and `.bin` outputs to `Release/Firmware`. If unavailable, leave firmware release empty and document the blocker; do not package stale binaries.

- [ ] **Step 3: Perform demo and real-hardware acceptance**

  Demo acceptance covers every visible state and failure path. Real hardware acceptance covers explicit masks `0x01`, `0x02`, `0x55`, `0xAA`, `0xC0`, `0xF0`, `0xFC`, `0xFF`, custom masks, side masters, presets, PWM, mapped current, disconnect/reconnect, and status readback. Never energize spraying hardware without the user's normal physical safety setup.

- [ ] **Step 4: Finalize portable context**

  Update all context documents with exact commands, test counts, hashes, unresolved hardware checks, relative paths, paired versions, and the first command a new account should run. Confirm no credential-like strings are present.

- [ ] **Step 5: Generate checksums and commit**

```powershell
Get-ChildItem .\Release -Recurse -File |
  Where-Object Name -ne 'SHA256SUMS.txt' |
  ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash, $_.FullName.Substring((Resolve-Path .).Path.Length + 1).Replace('\','/') } |
  Sort-Object | Set-Content .\Release\SHA256SUMS.txt -Encoding ascii
git add PROJECT_CONTEXT.md Docs Release
git commit -m "release: package paired sprayer touch HMI system"
```

- [ ] **Step 6: Final review**

  Run an independent requirements/protocol/code/visual review. Resolve all Critical and Important findings, rerun affected tests, and leave the worktree clean.
