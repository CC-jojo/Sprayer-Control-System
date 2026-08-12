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
                Assert.InRange(gauge.PreviewValue, gauge.SafetyMinimum, gauge.Maximum);
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

        Assert.Contains("ConfirmedMaskProperty", nozzleCode);
        Assert.Contains("PreviewMaskProperty", nozzleCode);
        Assert.Contains("IsPendingProperty", nozzleCode);
        Assert.Contains("CommandProperty", nozzleCode);
        Assert.Contains("byte displayedMask = IsPending ? PreviewMask : ConfirmedMask", nozzleCode);
        Assert.Contains("button.IsEnabled = !IsPending", nozzleCode);
        Assert.Contains("AutomationProperties.SetName", nozzleCode);
        Assert.Contains("AutomationProperties.SetHelpText", nozzleCode);
        Assert.Contains("AutomationProperties.SetItemStatus", nozzleCode);
        Assert.Equal(8, Count(nozzle, "NozzleCommandParameter Group="));
        Assert.Contains("Group=\"1\" Right=\"False\"", nozzle);
        Assert.Contains("Group=\"4\" Right=\"True\"", nozzle);
        Assert.Contains("Height=\"*\"", nozzle);
        Assert.Contains("Width=\"*\"", nozzle);
        Assert.Contains("<Ellipse", nozzle);
        Assert.DoesNotContain("ImageBrush", nozzle);
        Assert.DoesNotContain("Stretch=\"Fill\"", nozzle);
        Assert.Contains("Viewbox Stretch=\"Uniform\"", nozzle);
        Assert.Contains("喷嘴", nozzle);

        foreach (string property in new[] { "MinimumProperty", "MaximumProperty", "ValueProperty", "PreviewValueProperty", "SafetyMinimumProperty", "IsPendingProperty", "ReleaseCommandProperty" })
        {
            Assert.Contains(property, gaugeCode);
        }

        Assert.Contains("MouseLeftButtonUp", gauge);
        Assert.Contains("TouchUp", gauge);
        Assert.Contains("LostMouseCapture", gauge);
        Assert.Contains("LostTouchCapture", gauge);
        Assert.Contains("ReleaseCommand.Execute(PreviewValue)", gaugeCode);
        Assert.Contains("Math.Max(Minimum, SafetyMinimum)", gaugeCode);
        Assert.Contains("CoercePreviewValue", gaugeCode);
        Assert.Contains("if (IsPending)", gaugeCode);
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
        Assert.Contains("PWM总开关（上电默认开启）", xaml);
        Assert.DoesNotContain("PWM总开关（默认关闭）", xaml);
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

        Assert.Contains("连接后同步控制器实际状态", source);
        Assert.Contains("控制器继续按当前参数独立运行", source);
        Assert.DoesNotContain("PWM默认关闭", source);
        Assert.DoesNotContain("PWM由控制器安全关闭", source);

        int disconnectStart = source.IndexOf("private async Task DisconnectAsync", StringComparison.Ordinal);
        int clientStart = source.IndexOf("private ControllerClient Client", disconnectStart, StringComparison.Ordinal);
        string disconnectMethod = source[disconnectStart..clientStart];
        Assert.DoesNotContain("SetSwitch(false)", disconnectMethod);
    }

    private static string GetAppDirectory() => Path.GetFullPath(
        "../../../../SprayerControl.App", AppContext.BaseDirectory);

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
