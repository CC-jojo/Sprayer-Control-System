# Task 7 Brief: Dashboard, settings, log views, and visual acceptance

Start at approved HEAD `6d9d294d22635c6b2c8dfad852d42c0dca32d1cb`. Read project rules/context, design spec, approved UI reference requirements, Tasks 5–6 code, protocol/discovery code, Task 7 plan, and layout tests. Stop on unexpected tracked changes.

## Scope

- Replace `SprayerControl.App/MainWindow.xaml`; rewrite its code-behind as needed.
- Create `Views/DashboardView.xaml(.cs)`, `SettingsView.xaml(.cs)`, `LogView.xaml(.cs)`.
- Minimal Task 7 integration changes to existing App/ViewModels/services/tests are allowed only when a visible approved control needs real behavior (navigation, timestamps, controller-default save, connection lifecycle).
- Add a small deterministic UI-capture launcher/script if needed.
- Modify/add layout/smoke tests.
- Generate screenshots at `Temp/UIReview`; track durable review evidence/report under `Docs/UIReview` or the Task 7 report.
- Update context/progress/decisions/handoff and Task 7 brief/report.

Do not change firmware/register allocation, calibration mathematics, external baselines, or add unapproved features.

## Shell and lifecycle

- Full-bleed dark, borderless landscape shell; no white margin. Default production mode is maximized touch UI. Support `--windowed`, explicit width/height, `--demo`, and deterministic capture arguments without affecting production defaults.
- Header: `施药机控制系统`, live connection indicator, `设置`; content navigation for dashboard/settings/full log. No redundant side navigation.
- Production loads local config, preserves automatic serial discovery/retry/reconnect, creates `ModbusControllerSession` owning the discovered connection, refreshes status periodically, disposes cleanly, and returns to discovery on communication failure.
- Settings remains available while disconnected. Demo uses `DemoControllerSession`; any sample calibration/status is demo-only, visibly identified, and never persisted as a product default.

## Dashboard layout (match approved reference)

- Responsive star-sized three-column grid, approximately left 40%, center 24%, right 36%; adapt at 1366x768, 1600x900, 1920x1080, 1280x800, 1920x1200. No whole-window Viewbox/non-uniform stretch or fixed bitmap background.
- Left card `喷头布局与冠层高度`: reusable 4x2 full-circle nozzle array, group labels, canopy selector (`全部开启`, `第2+3+4组`, `第3+4组`, `第4组`), large left/right master buttons and confirmed/pending state.
- Center: `电磁阀占空比` radial gauge, PWM master/status; `回流阀开度` radial gauge, amber `安全下限`, safe-position note. Drag previews locally; release sends once.
- Right: `连接状态`, communication/current-module/firmware facts actually available; 4x2 authoritative left/right nozzle readback; `操作日志` newest five and `查看全部日志` navigation. Do not invent pressure, response time, alarms or diagnostics.
- Exact Chinese labels, large touch targets, no main-screen `TextBox`. Controller-confirmed state is authoritative. Legacy firmware clearly disables explicit nozzle controls.

## Settings

- Only approved cards: PWM lower bound, return safety minimum, ten opening/current calibration cells (`10%..100%`), curve preview, local save.
- Calibration cell opens `TouchNumericKeypad`; no physical keyboard dependency. Show validation/local-save result.
- If retaining `保存为上电默认值`, it must be separate, require explicit confirmation, and execute the existing real controller sequence (custom mask/current duty/current with persistence then save key); disable with a clear reason when disconnected/demo/invalid. Do not show a dead button.
- Saving local configuration updates the active Dashboard without restart. No default real calibration table.

## Full log

- Show timestamp and message for full ordered history, return button, and confirmed clear. No export/filter/search.
- Main dashboard always shows only newest five. Use a structured timestamped entry or equivalent deterministic model while preserving existing tested behavior.

## Visual acceptance and tests

TDD first. Assert shell full bleed, star columns, no main TextBox, resource styles, required Chinese labels, exact approved cards, settings-only keypad, log navigation, no prohibited categories, and responsive minimums. Add STA smoke tests for view construction/bindings where practical.

Provide a deterministic demo capture path and render PNGs at all six target sizes listed above into `Temp/UIReview`. Verify dimensions programmatically. Visually inspect at least 1366x768, 1280x800, 1920x1080, 1920x1200: no white borders, clipping/overlap, distorted circles, hidden touch controls, or illegible Chinese. Record file hashes and findings; keep at least representative durable evidence.

Run focused/full Release tests, Release build, capture verification, `git diff --check`. Report exact changed files/totals/screenshots/known hardware limits. Commit:

`feat: build responsive sprayer touchscreen views`

Return concise HEAD/totals/screenshots/report/concerns.
