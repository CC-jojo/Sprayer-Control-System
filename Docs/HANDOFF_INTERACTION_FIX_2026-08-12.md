# 新 UI 交互修正交接文档

## 1. 目标工程与当前基线

- 独立工程：`E:\CodexFILE\SprayerTouchHMI_2026-08-12`
- 当前分支：`master`
- 当前 HEAD：`3ed843fbcf9dec10169efe74c977dedcad3f7721`
- 当前工作树：干净，没有已应用的源代码修改。
- 旧桌面和 STM32 基线继续保持只读，不得修改：
  - `E:\CodexFILE\Sprayer controller system\SprayerDesktop`
  - `E:\CodexFILE\Sprayer controller system\SprayerController`

本轮只完成了问题定位，没有把试验性补丁写入目标工程。下一任务应从上述干净提交继续。

## 2. 用户最新确认的权威交互定义

以下定义覆盖现有设计文档中“八个喷头可单独触摸开关”的旧描述：

1. 左侧八个圆形喷头图案只是状态指示灯，只显示控制器读回的亮/灭状态，绝对不能点击、不能获得焦点、不能发送命令。
2. 真正允许操作的喷头控制入口只有：
   - `左侧喷头`总开关；
   - `右侧喷头`总开关；
   - `冠层高度`四档滑动阶梯开关。
3. `冠层高度`必须表现为可以触摸拖动/点击档位的纵向四档滑块，而不是四个普通按钮。四档从上到下：
   - 全部开启；
   - 第 2+3+4 组；
   - 第 3+4 组；
   - 第 4 组。
4. 中间真正允许操作的入口为：
   - 电磁阀占空比弧形滑动控件；
   - 电磁阀总开关；
   - 比例阀（回流阀开度）弧形滑动控件。
5. 所有设备状态仍以控制器读回为权威。触摸只产生预览，松手发送一次，等待控制器确认后更新显示。
6. 不删除固件和协议层已有的 8 位显式喷头掩码能力；只是主界面不再提供单个喷头的直接开关入口。

## 3. 已定位的根因

### 3.1 八个圆形喷头被错误实现为按钮

当前文件：

- `Source/SprayerDesktop/SprayerControl.App/Controls/NozzleArrayControl.xaml`
- `Source/SprayerDesktop/SprayerControl.App/Controls/NozzleArrayControl.xaml.cs`
- `Source/SprayerDesktop/SprayerControl.App/Views/DashboardView.xaml.cs`

现状：八个圆是 WPF `Button`，绑定 `NozzleCommandParameter` 和 `Nozzles.Command`，最终调用 `DashboardViewModel.ToggleNozzleAsync()`。这与用户最新确认的操作方式相反。

### 3.2 冠层高度被实现为四个按钮，而不是滑动阶梯

当前 `DashboardView.xaml` 中 `CanopySelector` 使用四个 `Button` 和 `Preset_Click`。外观上有竖线和圆点，但圆点 `IsHitTestVisible="False"`，实际没有滑块手势，因此用户认为“阶梯开关点不了/滑不了”是正确的。

### 3.3 两个弧形滑动控件及其他开关被禁用

`DashboardView.xaml.cs` 的 `RefreshPresentation()` 当前使用这些条件：

- 左右喷头和冠层按钮：必须控制器已连接、当前没有喷头命令等待、固件支持显式掩码。
- 两个弧形滑动控件：必须控制器已连接、十点标定配置有效、当前没有设定值命令等待。
- 电磁阀总开关：必须控制器已连接且当前没有设定值命令等待。

直接双击 `Release/Desktop/SprayerControl.App.exe` 会进入真实控制器自动发现模式。如果没有连接控制器，控件按安全逻辑全部禁用。比例阀十点标定不存在或无效时，两个弧形控件也会被禁用。

演示模式本身已有可交互的模拟控制器和示例标定，但发布目录没有明显的演示启动入口。可用参数为：

```powershell
SprayerControl.App.exe --demo --windowed
```

因此“看起来什么都点不了”的直接原因之一，是用户运行了生产入口而没有连接控制器；需要在发布包中增加明确的演示启动文件，不能通过取消生产安全禁用来掩盖问题。

## 4. 下一任务的精确修改范围

### A. 先写失败测试

修改：

`Source/SprayerDesktop/SprayerControl.Tests/MainWindowLayoutTests.cs`

至少断言：

- `NozzleArrayControl.xaml` 不包含 `<Button`、`NozzleCommandParameter` 或命令绑定；
- 八个状态指示元素存在，且 `IsHitTestVisible="False"`、`Focusable="False"`；
- `DashboardView.xaml.cs` 不包含 `Nozzles.Command` 和 `ToggleNozzleAsync`；
- `DashboardView.xaml` 包含纵向 `CanopySlider`，开启吸附档位和点按移动；
- 滑块松开事件同时支持鼠标与触摸；
- 发布工程包含演示启动文件并复制到 publish 目录。

先运行聚焦测试并确认按预期失败，再修改生产代码。

### B. 把八个圆改成纯状态指示器

- `NozzleArrayControl.xaml`：把八个 `Button` 改成不可命中的 `Grid`/`Border`/`ContentControl`，保留完整圆形和左右均匀布局。
- `NozzleArrayControl.xaml.cs`：删除 `CommandProperty`、`NozzleCommandParameter` 和按钮启用逻辑；只根据 `ConfirmedMask`/等待中的 `PreviewMask`更新颜色与自动化状态。
- 自动化帮助文字改成“只显示控制器确认状态，不可操作”。
- `DashboardView.xaml.cs`：删除 `Nozzles.Command` 绑定和 `Toggle()`。
- `DashboardViewModel.ToggleNozzleAsync()` 可暂时保留为协议/测试能力，但主界面不得调用；如删除，需同步现有单元测试，不能损害左右总开关、预设和显式掩码协议。

### C. 冠层高度改为纵向四档滑块

- 在 `CanopySelector` 中使用原生 WPF `Slider`：`Orientation="Vertical"`、范围 `1..4`、`TickFrequency="1"`、`IsSnapToTickEnabled="True"`、`IsMoveToPointEnabled="True"`。
- 建议映射：`4=全部开启`、`3=第2+3+4组`、`2=第3+4组`、`1=第4组`，这样顶部为全部、底部为第4组。
- 拖动时只预览档位；鼠标或触摸松开时调用一次 `ApplyCanopyPresetAsync()`。
- 发送期间禁用重复操作；控制器确认后再更新档位和八个圆的亮灭状态。
- 滑块启用条件与左右喷头总开关一致：已连接、支持显式掩码、无喷头命令等待。

### D. 保证真实控制入口可用且不破坏安全约束

- 左右喷头总开关继续使用当前 `SetLeftMasterAsync()` / `SetRightMasterAsync()`。
- 电磁阀总开关继续使用 `SetPwmEnabledAsync()`。
- 两个弧形滑动控件当前已经注册鼠标和触摸事件，保留“拖动预览、松手发送一次”。
- 不要为了演示而让真实未连接状态可发送命令。
- 比例阀没有有效十点标定时继续禁止设定值发送，并显示明确提示。
- PWM 是否应在缺少比例阀标定时仍可单独调节，需要在实现时审查：当前两个表盘共用 `CanCommitSetpoints`，所以缺少比例阀标定会连 PWM 占空比一起锁住。若用户希望 PWM 独立可调，应把配置有效条件拆分并增加测试；不要直接猜测。

### E. 增加清晰的演示启动入口

建议创建：

`Source/SprayerDesktop/SprayerControl.App/Start-Demo.cmd`

内容应从自身目录启动：

```bat
@echo off
start "" "%~dp0SprayerControl.App.exe" --demo --windowed
```

在 `.csproj` 中配置复制到 build/publish 目录。演示模式顶部必须继续明确显示“演示控制器已连接 / 样例数据不会保存”。生产 EXE 默认行为保持真实控制器自动发现。

## 5. 必须同步修正的文档冲突

以下文件目前仍宣称单个喷头可触摸控制，需要改成“只读状态指示”：

- `Docs/superpowers/specs/2026-08-12-sprayer-touch-hmi-design.md` 第 5 节；
- `Docs/REQUIREMENTS.md` 中对应八喷头交互条目；
- `PROJECT_CONTEXT.md` 的绑定需求摘要；
- `Docs/DECISIONS.md` 增加本次交互边界决定；
- `Docs/PROGRESS.md` 记录测试和修复结果；
- `Docs/HANDOFF.md` 更新下一账号的运行方法。

协议文档可继续保留显式 8 位掩码，因为左右总开关和冠层预设仍通过该掩码实现。

## 6. 验证与重新交付

完成后至少运行：

```powershell
dotnet test Source/SprayerDesktop/SprayerControl.Tests/SprayerControl.Tests.csproj --configuration Release --filter FullyQualifiedName~MainWindowLayoutTests
dotnet test Source/SprayerDesktop/SprayerControl.sln --configuration Release
dotnet build Source/SprayerDesktop/SprayerControl.sln --configuration Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/Capture-UIReview.ps1 -Configuration Release -OutputDirectory Temp/UIReview
```

还应在演示模式下人工验证：

1. 八个圆点击无反应且不获得焦点；
2. 左右喷头总开关可操作；
3. 冠层滑块四档可点、可拖、松手只发送一次；
4. 电磁阀总开关可操作；
5. 两个弧形控件可用鼠标和触摸拖动；
6. 等待确认时阻止重复发送；
7. 六个目标分辨率无裁切、圆形不变形；
8. 真实未连接模式仍安全禁用设备命令。

若形成新发布，建议版本升为 `1.0.1`，重新发布 `Release/Desktop`、更新发布说明和 `Release/SHA256SUMS.txt`。固件逻辑未变时可以继续配套现有固件，但必须重新验证三件固件产物与已验证构建一致，不得混入旧或未知二进制。

## 7. 本轮未完成事项

- 没有修改目标工程源代码；
- 没有生成新的桌面发布包；
- 没有更新 Release 版本或校验和；
- 没有进行实体控制器、触摸屏或喷雾输出验收。

下一任务的第一条命令：

```powershell
Get-Content -Raw .\Docs\HANDOFF_INTERACTION_FIX_2026-08-12.md
```
