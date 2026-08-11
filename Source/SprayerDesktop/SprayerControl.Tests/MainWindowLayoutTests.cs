namespace SprayerControl.Tests;

public sealed class MainWindowLayoutTests
{
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
}
