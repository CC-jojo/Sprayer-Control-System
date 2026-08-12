# Task 6 Brief: Responsive WPF design system and touch controls

Start at approved HEAD `6b345165995031414555badf9c72015f00d03d74`. Read project rules/context, design spec, Task 6 plan, approved reference requirements, current App/ViewModels, and layout tests. Expected untracked brief is allowed; stop on other tracked changes.

## Scope

- Modify `SprayerControl.App/App.xaml`.
- Create `Themes/Colors.xaml`, `Themes/Controls.xaml`.
- Create `Controls/NozzleArrayControl.xaml(.cs)`, `SegmentedGaugeControl.xaml(.cs)`, `TouchNumericKeypad.xaml(.cs)`.
- Modify `SprayerControl.Tests/MainWindowLayoutTests.cs`; add one focused control test file only if useful.
- Update `PROJECT_CONTEXT.md`, `Docs/PROGRESS.md`, `Docs/HANDOFF.md`, this brief/report.

Do not replace `MainWindow` or create final pages yet (Task 7). Therefore move the plan's final three-column/full-bleed/no-main-TextBox assertions to Task 7; Task 6 tests must validate resources and reusable controls without falsely claiming the old MainWindow is final. Do not edit ViewModels/protocol/firmware.

## Design tokens/styles

- Merge both dictionaries from `App.xaml` using stable dynamic resource keys.
- Named dark HMI brushes: background, panel, raised panel, border, primary/muted text, cyan, green, amber, red, disabled and focus/pending overlays.
- Reusable card, heading, touch button, status button/toggle and focus/disabled/pending styles. Minimum interactive size `56x56`; visible keyboard focus; no inline duplicated palette in controls.
- Chinese UI copy; English ASCII source paths/identifiers.

## Nozzle array

- Native WPF, no bitmap background. Four rows/groups and two evenly spaced columns; group labels; left=even mask bits and right=odd bits.
- Eight complete circular double-disc nozzle buttons (no arcs/half rings/spray dots), grey when off, orange when confirmed/preview on, cyan pending indication. Left/right whitespace and column spacing must be symmetric.
- Dependency properties for confirmed mask, preview mask, pending state and command. Each button passes a stable one-based group/side command parameter usable by Dashboard ViewModel. Minimum touch targets `56`.
- Any `Viewbox` is `Stretch="Uniform"` and wraps only the symbol, never the whole window/control layout.

## Segmented gauge

- Reusable reference-style segmented radial gauge (not a bitmap) with DPs: `Minimum`, `Maximum`, `Value`, `PreviewValue`, `SafetyMinimum`, `IsPending`, `ReleaseCommand`.
- Cyan normal active segments, amber safety region, muted inactive, centered numeric value; finite/clamped DP handling.
- Mouse/touch drag updates preview locally; exactly one release command on completed gesture with the preview value. Cancellation/lost capture sends nothing. Keyboard entry is not required.

## Numeric keypad

- Touch-only display plus buttons for `0..9`, decimal point, backspace, clear, cancel, confirm. No `TextBox`, physical-keyboard handler or automatic keyboard invocation.
- DPs for text and confirm/cancel commands; stable command parameter on confirm; prevent duplicate decimal separator and invalid characters. All keys meet `56` minimum.

## TDD/verification

Add XAML/source invariant tests first and record RED. Assert resource merge/key presence, touch sizes, no bitmap/no whole-control non-uniform stretch, nozzle 2x4 circular layout and fixed bit parameters, gauge DPs/release wiring, keypad keys/no TextBox, required Chinese labels. Build and run full Release tests; `git diff --check`. No serial/hardware required.

Report exact files/RED/GREEN/full/build totals and deferred Task 7 invariants. Commit:

`feat: add responsive WPF touch design system`

Return concise HEAD/totals/report/concerns.

## Implementation record

- RED recorded: the new resource/control invariant tests failed because `Themes/Colors.xaml` and `Controls/NozzleArrayControl.xaml` did not exist.
- GREEN recorded: focused Release layout/control tests passed 6/6; full Release desktop tests passed 166/166; Release build completed with 0 warnings and 0 errors.
- Deferred to Task 7: MainWindow replacement, final three-column/full-bleed/no-main-screen-TextBox assertions, and visual review at supported display sizes.
- Review remediation: pending controls now block duplicate interaction; nozzle preview is a complete pending target; gauge release is safety-clamped; nozzles and gauge have dynamic Chinese automation semantics.
- P1 remediation: an STA behavioral test covers lowering `Maximum` below `SafetyMinimum`; range-change coercion first reconciles safety and never supplies `Math.Clamp` with an inverted range.
