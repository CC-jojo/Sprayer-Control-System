using SprayerControl.App.Controls;

namespace SprayerControl.Tests;

public sealed class MainWindowLayoutTests
{
    [Fact]
    public void SegmentedGaugeKeepsSafetyAndPreviewValidWhenMaximumDropsBelowSafety()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var gauge = new SegmentedGaugeControl();
                gauge.Minimum = 0;
                gauge.Maximum = 100;
                gauge.SafetyMinimum = 90;
                gauge.Maximum = 80;

                Assert.Equal(80, gauge.Maximum);
                Assert.Equal(80, gauge.SafetyMinimum);
                // 预览值只受量程约束；安全下限仅用于着色提示（橙色安全区）。
                Assert.InRange(gauge.PreviewValue, gauge.Minimum, gauge.Maximum);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    [Fact]
    public void MergesTheReusableDarkTouchDesignSystem()
    {
        string appDirectory = GetAppDirectory();
        string app = File.ReadAllText(Path.Combine(appDirectory, "App.xaml"));
        string colors = File.ReadAllText(Path.Combine(appDirectory, "Themes", "Colors.xaml"));
        string controls = File.ReadAllText(Path.Combine(appDirectory, "Themes", "Controls.xaml"));

        Assert.Contains("Themes/Colors.xaml", app);
        Assert.Contains("Themes/Controls.xaml", app);
        Assert.Contains("HmiBackgroundBrush", colors);
        Assert.Contains("HmiPanelBrush", colors);
        Assert.Contains("HmiRaisedPanelBrush", colors);
        Assert.Contains("HmiBorderBrush", colors);
        Assert.Contains("HmiTextBrush", colors);
        Assert.Contains("HmiMutedTextBrush", colors);
        Assert.Contains("HmiCyanBrush", colors);
        Assert.Contains("HmiGreenBrush", colors);
        Assert.Contains("HmiAmberBrush", colors);
        Assert.Contains("HmiRedBrush", colors);
        Assert.Contains("HmiDisabledOverlayBrush", colors);
        Assert.Contains("HmiFocusOverlayBrush", colors);
        Assert.Contains("HmiPendingOverlayBrush", colors);
        Assert.Contains("TouchButtonStyle", controls);
        Assert.Contains("StatusButtonStyle", controls);
        Assert.Contains("HmiCardStyle", controls);
        Assert.Contains("HmiHeadingStyle", controls);
        Assert.Contains("MinWidth\" Value=\"56", controls);
        Assert.Contains("MinHeight\" Value=\"56", controls);
        Assert.Contains("FocusVisualStyle", controls);
    }

    [Fact]
    public void ReusableControlsKeepNativeTouchAndSafetyInvariants()
    {
        string controlsDirectory = Path.Combine(GetAppDirectory(), "Controls");
        string nozzle = File.ReadAllText(Path.Combine(controlsDirectory, "NozzleArrayControl.xaml"));
        string nozzleCode = File.ReadAllText(Path.Combine(controlsDirectory, "NozzleArrayControl.xaml.cs"));
        string gauge = File.ReadAllText(Path.Combine(controlsDirectory, "SegmentedGaugeControl.xaml"));
        string gaugeCode = File.ReadAllText(Path.Combine(controlsDirectory, "SegmentedGaugeControl.xaml.cs"));
        string keypad = File.ReadAllText(Path.Combine(controlsDirectory, "TouchNumericKeypad.xaml"));
        string keypadCode = File.ReadAllText(Path.Combine(controlsDirectory, "TouchNumericKeypad.xaml.cs"));
        string dashboard = File.ReadAllText(Path.Combine(GetAppDirectory(), "Views", "DashboardView.xaml"));
        string dashboardCode = File.ReadAllText(Path.Combine(GetAppDirectory(), "Views", "DashboardView.xaml.cs"));
        string appProject = File.ReadAllText(Path.Combine(GetAppDirectory(), "SprayerControl.App.csproj"));

        Assert.Contains("ConfirmedMaskProperty", nozzleCode);
        Assert.Contains("PreviewMaskProperty", nozzleCode);
        Assert.Contains("IsPendingProperty", nozzleCode);
        Assert.Contains("IsPreviewingProperty", nozzleCode);
        Assert.DoesNotContain("CommandProperty", nozzleCode);
        Assert.DoesNotContain("ICommand", nozzleCode);
        Assert.Contains("byte displayedMask = IsPending || IsPreviewing || IsPreStart ? PreviewMask : ConfirmedMask", nozzleCode);
        Assert.DoesNotContain(".IsEnabled", nozzleCode);
        Assert.Contains("AutomationProperties.SetName", nozzleCode);
        Assert.Contains("AutomationProperties.SetHelpText", nozzleCode);
        Assert.Contains("AutomationProperties.SetItemStatus", nozzleCode);
        Assert.Contains("点击切换喷头开关", nozzleCode);
        Assert.Contains("<Button", nozzle);
        Assert.DoesNotContain("NozzleCommandParameter", nozzle);
        Assert.Contains("Nozzle1LeftButton", nozzle);
        Assert.Contains("Nozzle1LeftIndicator", nozzle);
        Assert.Contains("Nozzle4RightIndicator", nozzle);
        Assert.Equal(8, Count(nozzle, "IsHitTestVisible=\"False\""));
        Assert.Equal(8, Count(nozzle, "Focusable=\"False\""));
        Assert.Contains("Height=\"*\"", nozzle);
        Assert.Contains("Width=\"*\"", nozzle);
        Assert.Contains("<Ellipse", nozzle);
        Assert.DoesNotContain("ImageBrush", nozzle);
        Assert.DoesNotContain("Stretch=\"Fill\"", nozzle);
        Assert.Contains("Viewbox Stretch=\"Uniform\"", nozzle);
        Assert.Contains("喷嘴", nozzle);

        Assert.DoesNotContain("Nozzles.Command", dashboardCode);
        Assert.Contains("ToggleNozzleAsync", dashboardCode);
        Assert.DoesNotContain("Preset_Click", dashboardCode);
        Assert.Contains("x:Name=\"CanopySlider\"", dashboard);
        Assert.Contains("Orientation=\"Vertical\"", dashboard);
        Assert.Contains("Minimum=\"1\"", dashboard);
        Assert.Contains("Maximum=\"4\"", dashboard);
        Assert.Contains("TickFrequency=\"1\"", dashboard);
        Assert.Contains("IsSnapToTickEnabled=\"True\"", dashboard);
        Assert.Contains("IsMoveToPointEnabled=\"True\"", dashboard);
        Assert.Contains("PreviewMouseLeftButtonUp", dashboard);
        Assert.Contains("PreviewTouchUp", dashboard);
        Assert.DoesNotContain("ApplyCanopyPresetAsync", dashboardCode);
        Assert.Contains("CanopySlider.IsEnabled", dashboardCode);
        Assert.DoesNotContain("CanopySelector", dashboard);
        Assert.Contains("Start-Demo.cmd", appProject);

        foreach (string property in new[] { "MinimumProperty", "MaximumProperty", "ValueProperty", "PreviewValueProperty", "SafetyMinimumProperty", "IsPendingProperty", "ReleaseCommandProperty", "SafetyFloorCommandProperty" })
        {
            Assert.Contains(property, gaugeCode);
        }

        // 拖动/滑动手势已移除，保留显示与加减按钮。
        Assert.DoesNotContain("OnMouseLeftButtonDown", gauge);
        Assert.DoesNotContain("OnTouchDown", gauge);
        Assert.DoesNotContain("ReleaseCommand.Execute", gaugeCode);
        Assert.Contains("Math.Max(Minimum, SafetyMinimum)", gaugeCode);
        Assert.Contains("CoercePreviewValue", gaugeCode);
        Assert.Contains("segment.Opacity = IsPending ? 0.62d : 1d", gaugeCode);
        Assert.Contains("StepDownCommand", gaugeCode);

        // 安全下限按钮：位于减号与加号之间、数字圆盘下方，只在绑定命令的仪表上显示。
        Assert.Contains("SafetyFloorButton", gauge);
        Assert.Contains("Content=\"安全下限\"", gauge);
        Assert.Contains("HorizontalAlignment=\"Center\"", gauge);
        Assert.Contains("OpeningGauge.SafetyFloorCommand", dashboardCode);
        Assert.Contains("ApplySafetyFloor", dashboardCode);
        Assert.Contains("ApplySafetyFloorAsync", dashboardCode);
        Assert.Contains("AutomationProperties.SetName", gaugeCode);
        Assert.Contains("AutomationProperties.SetHelpText", gaugeCode);
        Assert.Contains("AutomationProperties.SetItemStatus", gaugeCode);
        Assert.DoesNotContain("ImageBrush", gauge);

        Assert.Contains("TextProperty", keypadCode);
        Assert.Contains("ConfirmCommandProperty", keypadCode);
        Assert.Contains("CancelCommandProperty", keypadCode);
        Assert.Contains("CommandParameter", keypad);
        Assert.Contains("确认", keypad);
        Assert.Contains("取消", keypad);
        Assert.Contains("退格", keypad);
        Assert.Contains("清除", keypad);
        Assert.DoesNotContain("TextBox", keypad);
        Assert.DoesNotContain("KeyDown", keypadCode);
        Assert.DoesNotContain("Keyboard", keypadCode);
    }

    [Fact]
    public void ReturnValveGaugeExposesOneTouchSafetyFloorButton()
    {
        string controlsDirectory = Path.Combine(GetAppDirectory(), "Controls");
        string gauge = File.ReadAllText(Path.Combine(controlsDirectory, "SegmentedGaugeControl.xaml"));
        string gaugeCode = File.ReadAllText(Path.Combine(controlsDirectory, "SegmentedGaugeControl.xaml.cs"));
        string dashboardCode = File.ReadAllText(Path.Combine(GetAppDirectory(), "Views", "DashboardView.xaml.cs"));

        // 控件提供可选的一键安全下限命令，按钮位于减号与加号之间、数字圆盘正下方。
        Assert.Contains("SafetyFloorCommandProperty", gaugeCode);
        Assert.Contains("SafetyFloorButtonStyle", gauge);
        Assert.Contains("x:Name=\"SafetyFloorButton\"", gauge);
        Assert.Contains("Content=\"安全下限\"", gauge);

        // 只有回流阀仪表绑定该命令，占空比仪表不绑定；切换逻辑在视图模型里。
        Assert.Contains("OpeningGauge.SafetyFloorCommand", dashboardCode);
        Assert.DoesNotContain("DutyGauge.SafetyFloorCommand", dashboardCode);
        Assert.Contains("ApplySafetyFloorAsync", dashboardCode);
    }

    [Fact]
    public void DoesNotExposeManualSerialPortControls()
    {
        string path = Path.GetFullPath(
            "../../../../SprayerControl.App/MainWindow.xaml", AppContext.BaseDirectory);
        string xaml = File.ReadAllText(path);

        Assert.DoesNotContain("PortBox", xaml);
        Assert.DoesNotContain("ConnectButton", xaml);
        Assert.DoesNotContain("RefreshPorts_Click", xaml);
        Assert.DoesNotContain("Connect_Click", xaml);
        Assert.DoesNotContain("<ComboBox", xaml);
        string dashboard = File.ReadAllText(Path.Combine(GetAppDirectory(), "Views", "DashboardView.xaml"));
        Assert.Contains("PWM总开关", dashboard);
        Assert.DoesNotContain("默认关闭", dashboard);
    }

    [Fact]
    public void UsesAsynchronousTimeoutSafePortDiscovery()
    {
        string appDirectory = Path.GetFullPath(
            "../../../../SprayerControl.App", AppContext.BaseDirectory);
        string discovery = File.ReadAllText(
            Path.Combine(appDirectory, "WindowsSerialPortDiscovery.cs"));
        string window = File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml.cs"));

        Assert.Contains("GetPortsAsync", discovery);
        Assert.Contains("SerialPortCatalog.BuildAsync", discovery);
        Assert.Contains("await WindowsSerialPortDiscovery.GetPortsAsync(token)", window);
        Assert.DoesNotContain("WindowsSerialPortDiscovery.GetPorts()", window);
    }

    [Fact]
    public void DescribesStandaloneControllerOperation()
    {
        string path = Path.GetFullPath(
            "../../../../SprayerControl.App/MainWindow.xaml.cs", AppContext.BaseDirectory);
        string source = File.ReadAllText(path);

        Assert.Contains("周期性真实状态刷新", source);
        Assert.Contains("控制器继续按当前参数独立运行", source);
        Assert.Contains("已自动保存本地配置", source);
        Assert.DoesNotContain("PWM默认关闭", source);
        Assert.DoesNotContain("PWM由控制器安全关闭", source);

        int disconnectStart = source.IndexOf("private async Task ReturnToDiscoveryAsync", StringComparison.Ordinal);
        int configurationStart = source.IndexOf("private void ApplyConfiguration", disconnectStart, StringComparison.Ordinal);
        string disconnectMethod = source[disconnectStart..configurationStart];
        Assert.DoesNotContain("SetPwmEnabledAsync(false)", disconnectMethod);
    }

    [Fact]
    public void ComposesResponsiveFullBleedDashboardSettingsAndLogViews()
    {
        string appDirectory = GetAppDirectory();
        string shell = File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml"));
        string dashboard = File.ReadAllText(Path.Combine(appDirectory, "Views", "DashboardView.xaml"));
        string settings = File.ReadAllText(Path.Combine(appDirectory, "Views", "SettingsView.xaml"));
        string log = File.ReadAllText(Path.Combine(appDirectory, "Views", "LogView.xaml"));

        Assert.Contains("WindowStyle=\"None\"", shell);
        Assert.Contains("WindowState=\"Maximized\"", shell);
        Assert.Contains("HmiBackgroundBrush", shell);
        Assert.Contains("DashboardView", shell);
        Assert.Contains("SettingsView", shell);
        Assert.Contains("LogView", shell);
        Assert.DoesNotContain("<TextBox", shell);
        Assert.Contains("38*", dashboard);
        Assert.Contains("26*", dashboard);
        Assert.Contains("36*", dashboard);
        Assert.Contains("喷头开关控制", dashboard);
        Assert.Contains("NozzleArrayControl", dashboard);
        Assert.Contains("SegmentedGaugeControl", dashboard);
        Assert.DoesNotContain("CanopySelector", dashboard);
        Assert.Contains("NozzleReadbackGrid", dashboard);
        Assert.Contains("ConnectionStateDot", dashboard);
        Assert.Contains("ErrorText", dashboard);
        Assert.Contains("DutyGauge.IsEnabled", File.ReadAllText(Path.Combine(appDirectory, "Views", "DashboardView.xaml.cs")));
        Assert.Contains("OpeningGauge.IsEnabled", File.ReadAllText(Path.Combine(appDirectory, "Views", "DashboardView.xaml.cs")));
        Assert.Contains("HasCommunicationFault", File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml.cs")));
        Assert.DoesNotContain("!string.IsNullOrWhiteSpace(_dashboard.ErrorText)", File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml.cs")));
        Assert.Contains("运行状态", dashboard);
        Assert.Contains("安全位置", dashboard);
        Assert.Contains("查看全部日志", dashboard);
        Assert.Contains("查看全部日志", dashboard);
        Assert.DoesNotContain("TextBox", dashboard);
        Assert.Contains("TouchNumericKeypad", settings);
        Assert.Contains("PWM占空比下限", settings);
        Assert.Contains("回流阀安全下限", settings);
        Assert.Contains("保存本地配置", settings);
        Assert.Contains("CurveCanvas", settings);
        Assert.Contains("MappedCurrentText", settings);
        Assert.Contains("确认安全下限修改", settings);
        Assert.Contains("保存为上电默认值", settings);
        Assert.Contains("ControllerDefaultReason", settings);
        Assert.DoesNotContain("压力", settings);
        Assert.Contains("返回", log);
        Assert.Contains("清空日志", log);
    }

    private static string GetAppDirectory() => Path.GetFullPath(
        "../../../../SprayerControl.App", AppContext.BaseDirectory);

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
