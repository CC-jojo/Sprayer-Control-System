# Sprayer Touch HMI Design Specification

## 1. Objective

Build a new, independently maintainable sprayer-control project containing both the Windows touchscreen host application and its matching STM32 firmware. The existing projects remain immutable baselines. The new system reproduces the user-approved dark three-column HMI as real WPF controls and extends the protocol/firmware for eight independently controllable nozzles.

## 2. Project Isolation

- New root: `E:/CodexFILE/SprayerTouchHMI_2026-08-12`
- Desktop baseline, read-only source: `E:/CodexFILE/Sprayer controller system/SprayerDesktop`
- Firmware baseline, read-only source: `E:/CodexFILE/Sprayer controller system/SprayerController`
- New editable copies:
  - `Source/SprayerDesktop`
  - `Source/SprayerController`
- Copy operations must not delete, move, rename, overwrite, or edit either baseline.
- Before editing, record baseline file counts and SHA-256 manifests, copy both sources, and verify copied manifests.
- Initialize local Git only inside the new project after the verified copy; no remote, account token, or credential is configured automatically.

## 3. Supported Platform and Display

- Desktop technology remains `.NET 8` WPF on Windows.
- Primary use is a cab-mounted touchscreen; mouse operation remains naturally possible but is not the interaction baseline.
- Reference canvas is `1920×1080` landscape.
- The UI must also support `1366×768`, `1600×900`, `1280×800`, and `1920×1200`, including Windows display scaling.
- Layout uses responsive WPF `Grid` star sizing and bounded minimum sizes. Circular controls use uniform aspect-preserving containers; the whole screen must not be stretched non-uniformly.
- At 16:10, extra vertical space is assigned to logs and spacing rather than stretching circles.
- Primary touch targets are at least `56×56` device-independent pixels; frequently used controls target `64×64` or larger.
- The main screen uses no text-entry fields. Numeric entry is limited to the settings screen and uses an on-screen numeric keypad.

## 4. Visual and Information Architecture

The approved reference establishes a dark industrial/agricultural HMI with a full-bleed background and three columns:

1. Left: eight-nozzle diagram, canopy-height mode selector, left-side and right-side master controls.
2. Center: electromagnetic-valve duty control and return-valve opening control.
3. Right: controller connection, eight-channel operating state, five recent log entries, and access to all logs.

The reference image is a design specification, not a bitmap background. Every visible item must be a native WPF control, shape, icon, or text element. No white outer border, monitor bezel, watermark, or decorative control is added.

Normal inactive states use restrained gray. Cyan identifies selection/adjustment, green confirms active/connected state, amber marks limits/caution, and red is reserved for actual faults.

## 5. Eight-Nozzle Control Model

- The system exposes eight real nozzle channels: four vertical groups, each with left and right nozzles.
- The eight rings are pure status indicators: they are not individual touch controls, are never focusable or hit-testable, and their displayed state always comes from controller-confirmed status (or the canopy-height drag preview while adjusting).
- Left-side master control affects all four left nozzles; right-side master control affects all four right nozzles.
- Canopy-height presets remain available:
  - Group 4
  - Groups 3+4
  - Groups 2+3+4
  - All groups
- Presets are convenience operations that calculate an eight-bit target mask while respecting the current left/right side enable selections.
- Individual nozzle changes update the explicit mask and may result in a custom pattern; the UI must display the real mask without forcing it back to a named preset.
- Rows 1–2 appear gray when off and rows 3–4 appear orange in the approved example state. Color always follows actual controller feedback.

## 6. Electromagnetic-Valve PWM

- Preserve the existing PWM master switch and unified duty-cycle behavior.
- Replace text entry and the separate Apply button with a touch slider/segmented circular control plus fine-adjust buttons.
- Dragging previews the value locally. Releasing sends the new value immediately.
- The configured lower bound is editable in Settings; the upper bound remains fixed at `80%` until hardware thermal testing explicitly approves a different value.
- The displayed operating range is `configured lower bound ... 80%`.
- After transmission, show a pending state. Only controller confirmation turns the state green. Failure restores the last confirmed value and reports the communication error.

## 7. Return-Valve Calibration and Safety Lower Limit

### 7.1 Ten-point calibration

- The main screen displays return-valve opening as a percentage; it does not require the operator to enter current.
- Settings contains exactly ten editable calibration points: `10%, 20%, 30%, 40%, 50%, 60%, 70%, 80%, 90%, 100%`.
- Each point stores a current value in mA. No default field data is represented as validated calibration data.
- Values between points use piecewise-linear interpolation.
- The settings page shows a curve preview and the mapped current for the selected opening.
- Calibration values are edited with the on-screen numeric keypad.
- The abandoned special `0% signal cut-off` feature is not implemented and requires no new hardware cut-off circuit.

### 7.2 Safety lower limit

- Settings provides a user-defined safety minimum opening. `30%` is only the approved reference example, not a hard-coded universal limit.
- The main opening control is limited to `safety minimum ... 100%`; values below the minimum are visibly disabled and cannot be selected.
- If the minimum is raised above the current opening, the setpoint moves smoothly to the new minimum rather than jumping abruptly.
- Changing the minimum requires confirmation and writes an operation-log entry containing the old and new values.
- `100%` is defined in the UI as return fully open and the safe-position end of the control.

### 7.3 Local configuration

- Ten-point calibration, safety minimum, and PWM duty lower bound are stored on the host computer, not in STM32 Flash.
- Save uses validation and an atomic replace strategy so a partial write cannot corrupt the active configuration.
- Missing, malformed, non-finite, out-of-range, or non-monotonic configuration blocks normal return-valve adjustment and displays `比例阀尚未完成标定`.
- Configuration contains a schema version and last-modified timestamp but no user account, API key, token, or machine secret.

## 8. Settings and Logs

- Settings uses large touch cards and contains only confirmed functions:
  - PWM duty lower bound
  - Return-valve safety minimum
  - Ten-point opening-current calibration
  - Calibration curve preview
  - Local save
  - Existing save-as-power-on-default capability, kept behind confirmation and clearly separated from local calibration save
- Do not add pressure display, pressure alarms, communication diagnostics pages, field-test output, restore buttons, company branding, or unrelated menus unless separately specified later.
- The main screen shows the latest five timestamped operations.
- `查看全部日志` opens a touch-friendly full log view. Existing clear-log behavior remains available there rather than on the main screen.

## 9. Desktop Architecture

Keep `SprayerControl.Core` as the protocol/domain layer and preserve the working serial discovery, Modbus RTU, retries, handshake, heartbeat, disconnect, and reconnect behavior.

Add focused units:

- `SprayerControl.Core`: eight-bit nozzle mask, protocol versioning, controller commands/status, calibration value objects and validation.
- `SprayerControl.App/ViewModels`: screen state, commands, pending/confirmed/failed transitions, responsive-page coordination.
- `SprayerControl.App/Controls`: nozzle diagram, segmented gauge, status tiles, touch numeric keypad.
- `SprayerControl.App/Services`: local calibration store, log view adapter, controller-session adapter, and demo/fake session for hardware-free UI verification.
- `SprayerControl.App/Views`: dashboard, settings, and full log view.

The existing code-behind communication behavior is first protected by tests, then moved behind interfaces incrementally. A full unrelated framework rewrite is out of scope.

## 10. Firmware and Protocol Architecture

- Preserve the STM32F103C8T6 firmware layers (`Core`, `App`, `Platform`) and existing Modbus RTU transport.
- Extend the writable register model with an explicit eight-bit commanded nozzle mask and a protocol/feature version detectable during handshake.
- Continue returning the actual output mask in status so the desktop can render confirmed channel states.
- Update `HostRegisters`, `PwmEngine` integration, parameter validation, configuration compatibility, and tests for all eight bits.
- Legacy canopy modes may remain internally for compatibility, but the new desktop communicates the explicit mask for independent operation.
- Existing PWM master, duty limits, current-module output, communication-stop indication, watchdog integration, and configuration CRC behavior remain intact unless a test-backed protocol change requires an update.
- Firmware Flash does not store the host's ten-point calibration table.

## 11. Command and Feedback Flow

1. Operator touches or drags a control.
2. UI previews the proposed state without claiming controller confirmation.
3. On tap completion or drag release, the ViewModel sends one validated command.
4. The control shows pending state and prevents conflicting duplicate writes.
5. The controller replies and the desktop reads back status.
6. Confirmed feedback updates the UI and appends a timestamped log.
7. Timeout/protocol failure restores the last confirmed visual state, records failure, and follows the existing reconnect path when appropriate.

Automatic periodic status reads replace a manual `读取状态` workflow on the dashboard.

## 12. Testing and Acceptance

### Desktop

- Preserve and pass all existing `SprayerControl.Tests` tests.
- Add tests for mask composition, side masters, canopy presets, custom masks, calibration interpolation, configuration validation/atomic load behavior, safety minimum, pending/rollback state, and responsive XAML invariants.
- Build `Debug` and `Release` for `net8.0-windows`.
- Verify dashboard and settings at all supported resolutions and at common Windows scaling values.

### Firmware

- Add host-side tests for register writes/reads, all eight mask bits, invalid masks/register values, mode compatibility, PWM output mask, persistence compatibility, and status feedback.
- Run existing firmware tests and CMake builds where the installed compiler permits. If the compiler is unavailable, report that limitation without claiming a fresh firmware build.

### Integrated acceptance

- Hardware-free demo mode must exercise all visible states.
- Real-controller acceptance covers discovery, connection, eight individual nozzles, side masters, presets, PWM duty, return-valve mapping, disconnect/reconnect, malformed configuration, and logs.
- A release is not complete until the published desktop application and corresponding firmware artifact are clearly paired by version.

## 13. Portability and Handoff

The project root maintains:

- `AGENTS.md`: binding rules for future agents/accounts.
- `PROJECT_CONTEXT.md`: architecture, baselines, confirmed requirements, current state, build/test commands, and next actions.
- `Docs/REQUIREMENTS.md`: approved functional requirements.
- `Docs/DECISIONS.md`: durable decisions and reasons.
- `Docs/PROGRESS.md`: completed/current/next work and verification evidence.
- `Docs/PROTOCOL.md`: Modbus register and compatibility specification.
- `Docs/HANDOFF.md`: migration checklist for a new computer, account, or task.

These files use relative project paths where possible and never contain credentials. They are updated at every reviewed implementation milestone.

## 14. Delivery Structure

- `Source`: editable desktop and firmware source.
- `Temp`: scratch outputs and intermediate artifacts.
- `Release`: paired desktop and firmware deliverables plus checksums.
- `Assets`: approved UI references and icons.
- `Docs`: specifications, requirements, protocol, progress, decisions, and handoff instructions.

The previous projects remain available for comparison and rollback and are never treated as disposable temporary copies.
