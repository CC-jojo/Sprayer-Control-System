using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SprayerControl.App.Helpers;
using SprayerControl.App.Services;
using SprayerControl.App.ViewModels;
using SprayerControl.Core;

namespace SprayerControl.App;

/// <summary>触摸外壳负责导航、发现、会话生命周期和周期性真实状态刷新。</summary>
public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly LogViewModel _log = new();
    private readonly IHostConfigurationStore _store = new JsonHostConfigurationStore();
    private readonly PortMemoryStore _portMemory = new();
    private IControllerSession? _session;
    private DashboardViewModel? _dashboard;
    private SettingsViewModel? _settings;
    private HostConfiguration? _activeConfiguration;
    private Task? _discoveryTask;
    private bool _refreshing;
    private bool _startupRestoreApplied;
    private readonly bool _demo;

    public MainWindow()
    {
        InitializeComponent();
        _demo = App.DemoMode;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _refreshTimer.Tick += RefreshTimer_Tick;
        Dashboard.ShowLogRequested += ShowLog;
        Settings.BackRequested += ShowDashboard;
        Settings.ConfigurationSaved += ApplyConfiguration;
        Log.BackRequested += ShowDashboard;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 上机自适应：按实际屏幕分辨率/DPI 缩放设计稿并铺满工作区；截图模式仅缩放内容。
        AdaptiveLayoutManager.Apply(this, RootLayout, capture: App.CapturePath is not null);
        if (App.CapturePath is not null)
        {
            WindowState = WindowState.Normal; Width = App.CapturePixelWidth; Height = App.CapturePixelHeight;
            Left = -20000; Top = -20000; UpdateLayout();
        }
        await CreateViewModelsAsync(_lifetime.Token);
        _refreshTimer.Start();
        if (_demo) SetHeaderConnection("演示控制器已连接", connected: true);
        if (App.CapturePath is not null)
        {
            _ = Dispatcher.BeginInvoke(() => App.Capture(this), DispatcherPriority.ApplicationIdle);
            return;
        }
        if (_demo) return;
        StartDiscovery();
    }

    private async Task CreateViewModelsAsync(CancellationToken token)
    {
        HostConfiguration? configuration;
        if (_demo)
            configuration = CreateDemoConfiguration();
        else
        {
            var loader = new SettingsViewModel(_store);
            await loader.LoadAsync(token);
            configuration = loader.SavedConfiguration;
        }

        _activeConfiguration = configuration;
        _session = _demo ? new DemoControllerSession() : new DisconnectedControllerSession();
        _dashboard = new DashboardViewModel(_session, configuration, _log);
        _settings = new SettingsViewModel(_store, configuration, allowPersistence: !_demo);
        Dashboard.Bind(_dashboard, _demo);
        Settings.Bind(_settings, _demo);
        Log.Bind(_log);

        if (_demo)
        {
            _dashboard.PreviewDuty = 60;
            _dashboard.PreviewReturnOpening = 100;
            await _session.SetNozzleMaskAsync(NozzleMask.ForPreset(CanopyPreset.Groups34, true, true), token);
            await _session.SetPwmEnabledAsync(true, token);
            await _dashboard.CommitReturnOpeningAsync(token);
            _log.Add("系统启动完成，演示控制器已连接");
            _log.Add("电磁阀总开关 已开启");
            _log.Add("电磁阀占空比设为 60%");
            _log.Add("喷头模式切换为 第3+4组");
            _log.Add("回流阀开度设为 100%（安全全开）");
        }
        UpdateSettingsControllerAction();
    }

    private void StartDiscovery()
    {
        if (!_lifetime.IsCancellationRequested && (_discoveryTask is null || _discoveryTask.IsCompleted))
            _discoveryTask = DiscoverAsync(_lifetime.Token);
    }

    private async Task DiscoverAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _session is DisconnectedControllerSession)
        {
            try
            {
                SetHeaderConnection("正在自动查找控制器", connected: false);
                SerialPortDescriptor[] ports = await WindowsSerialPortDiscovery.GetPortsAsync(token);
                string? preferredPort = _portMemory.Load();
                ControllerConnection? found = await Task.Run(() => ControllerDiscovery.TryDiscoverAsync(
                    ports, port => new ReliableModbusChannel(new SerialModbusChannel(port.PortName)), token,
                    preferredPortName: preferredPort), token);
                if (found is null)
                {
                    await Task.Delay(2000, token);
                    continue;
                }
                _portMemory.Save(found.Port.PortName);
                var session = await ModbusControllerSession.CreateAsync(found.Client, found, token);
                await ReplaceSessionAsync(session, token);
                SetHeaderConnection("控制器已连接", connected: true);
                _log.Add("控制器已发现并完成连接。");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                _log.Add($"自动发现失败：{exception.Message}");
                SetHeaderConnection("通信异常，正在重试", connected: false, error: true);
                await Task.Delay(2000, token);
            }
        }
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_refreshing || _dashboard is null || _session is null || !_session.IsConnected) return;
        _refreshing = true;
        try
        {
            await _dashboard.RefreshAsync(_lifetime.Token);
            if (_dashboard.HasCommunicationFault && !_demo)
                await ReturnToDiscoveryAsync();
        }
        finally { _refreshing = false; }
    }

    private async Task ReplaceSessionAsync(IControllerSession session, CancellationToken token)
    {
        IControllerSession? old = _session;
        session.UpdateNozzleMapping(_settings?.SavedConfiguration?.NozzleMapping ?? NozzleMask.DefaultUiToPhysicalBit);
        _session = session;
        _dashboard = new DashboardViewModel(session, _settings?.SavedConfiguration, _log);
        Dashboard.Bind(_dashboard, false);
        if (_settings is not null)
            _settings.PwmDutyMaximum = session.ParameterLimits.DutyMaximum;
        await _dashboard.RefreshAsync(token);
        if (old is not null) await old.DisposeAsync();

        /* 每次连接成功都自动"保存本地配置"一次（等同点击设置页保存按钮）：
           标定未填满时静默跳过（不打扰）；异常仅记日志；演示模式禁止写本地。 */
        if (!_demo && _settings is not null && _settings.IsCalibrationComplete)
        {
            try
            {
                bool saved = await _settings.SaveAsync(token);
                if (saved && _settings.SavedConfiguration is not null)
                {
                    ApplyConfiguration(_settings.SavedConfiguration);
                    _log.Add("已自动保存本地配置。");
                }
            }
            catch (Exception exception)
            {
                _log.Add($"自动保存本地配置失败：{exception.Message}");
            }
        }

        /* 启动恢复（仅首次连接成功时执行一次）：
           占空比恢复到上次记忆值（按 [下限,上限] 钳制，防止越界导致提交被拒），
           回流阀开度自动设为 100%（安全全开）并下发。 */
        if (!_startupRestoreApplied && _settings?.SavedConfiguration is HostConfiguration restore)
        {
            _startupRestoreApplied = true;
            int dutyMinimum = _dashboard.PwmDutyMinimum;
            int dutyMaximum = Math.Max(dutyMinimum, _dashboard.PwmDutyMaximum);
            int restoredDuty = Math.Clamp(
                restore.LastDutyPercent ?? _dashboard.ConfirmedDuty,
                dutyMinimum,
                dutyMaximum);
            _dashboard.PreviewDuty = restoredDuty;
            /* 回流阀启动值：开关打开 → 上电默认开度；开关关闭 → 继承上次开度
               （没有记忆值时退回 100% 安全全开）。低于安全下限时由仪表盘自动钳制。 */
            double restoredOpening = restore.PowerOnReturnValveEnabled
                ? restore.PowerOnReturnValvePercent
                : restore.LastReturnOpeningPercent ?? 100;
            _dashboard.PreviewReturnOpening = restoredOpening;
            try
            {
                await _dashboard.CommitReturnOpeningAsync(token);
                _log.Add(restore.PowerOnReturnValveEnabled
                    ? $"启动恢复：电磁阀占空比 {_dashboard.ConfirmedDuty}%，回流阀开度 {_dashboard.ConfirmedReturnOpening:F0}%（上电默认值）。"
                    : $"启动恢复：电磁阀占空比 {_dashboard.ConfirmedDuty}%，回流阀开度 {_dashboard.ConfirmedReturnOpening:F0}%（继承上次开度）。");
            }
            catch (Exception exception)
            {
                _log.Add($"启动恢复失败：{exception.Message}");
            }
        }

        UpdateSettingsControllerAction();
    }

    private async Task ReturnToDiscoveryAsync()
    {
        IControllerSession? old = _session;
        _session = new DisconnectedControllerSession();
        _dashboard = new DashboardViewModel(_session, _settings?.SavedConfiguration, _log);
        Dashboard.Bind(_dashboard, false);
        SetHeaderConnection("通信中断，正在重新连接", connected: false, error: true);
        _log.Add("通信中断；控制器继续按当前参数独立运行，重新扫描。");
        if (old is not null) await old.DisposeAsync();
        UpdateSettingsControllerAction();
        StartDiscovery();
    }

    private void ApplyConfiguration(HostConfiguration configuration)
    {
        int? oldSafety = _activeConfiguration?.ReturnValveSafetyMinimum;
        _activeConfiguration = configuration;
        _dashboard?.UpdateConfiguration(configuration);
        _session?.UpdateNozzleMapping(configuration.NozzleMapping);
        if (oldSafety.HasValue && oldSafety.Value != configuration.ReturnValveSafetyMinimum)
            _log.Add($"回流阀安全下限由 {oldSafety}% 修改为 {configuration.ReturnValveSafetyMinimum}%（已确认并保存）");
        else
            _log.Add("本地配置已保存并应用到仪表盘。");
        UpdateSettingsControllerAction();
    }

    private void UpdateSettingsControllerAction()
    {
        bool available = !_demo && _session?.IsConnected == true && _session.Capabilities.SupportsExplicitNozzleMask && _settings?.SavedConfiguration is not null;
        string reason = _demo ? "演示模式禁止写入控制器。"
            : _session?.IsConnected != true ? "控制器未连接。"
            : !_session.Capabilities.SupportsExplicitNozzleMask ? "控制器固件不支持独立八喷嘴上电默认值。"
            : "请先保存有效的本地配置。";
        Settings.ConfigureControllerDefaults(available, reason, available ? SaveControllerDefaultsAsync : null);
    }

    private async Task SaveControllerDefaultsAsync(CancellationToken token)
    {
        if (_demo || _session?.IsConnected != true || _dashboard is null || _settings?.SavedConfiguration is not HostConfiguration configuration)
            throw new InvalidOperationException("当前状态不允许保存控制器上电默认值。");
        /* 回流阀上电默认值：开关打开用设置页设定的开度（不再写死 100%）；
           开关关闭时用当前确认开度，让控制器单独上电也尽量接近"继承上次状态"。 */
        double powerOnOpening = configuration.PowerOnReturnValveEnabled
            ? configuration.PowerOnReturnValvePercent
            : _dashboard.ConfirmedReturnOpening;
        double current = configuration.ReturnValveCalibration.CurrentMilliampsAt(powerOnOpening, configuration.ReturnValveSafetyMinimum);
        ushort microamps = checked((ushort)Math.Round(current * 1000, MidpointRounding.AwayFromZero));
        await _session.SavePowerOnDefaultsAsync(_dashboard.ConfirmedMask, (ushort)_dashboard.ConfirmedDuty, microamps, token);
        _log.Add(configuration.PowerOnReturnValveEnabled
            ? $"喷嘴掩码、占空比和回流阀电流已保存为控制器上电默认值（回流阀上电为 {powerOnOpening:F0}%）。"
            : $"喷嘴掩码、占空比和回流阀电流已保存为控制器上电默认值（回流阀上电为当前开度 {powerOnOpening:F0}%）。");
    }

    private void SetHeaderConnection(string text, bool connected, bool error = false)
    {
        ConnectionText.Text = text;
        HeaderConnectionDot.Fill = (Brush)FindResource(error ? "HmiRedBrush" : connected ? "HmiGreenBrush" : "HmiAmberBrush");
        ConnectionText.Foreground = (Brush)FindResource(error ? "HmiRedBrush" : connected ? "HmiGreenBrush" : "HmiMutedTextBrush");
    }

    private void ShowSettings_Click(object sender, RoutedEventArgs e) => Show(Settings);
    private void ShowLog() => Show(Log);
    private void ShowDashboard() => Show(Dashboard);
    private void Show(UIElement target)
    {
        Dashboard.Visibility = ReferenceEquals(target, Dashboard) ? Visibility.Visible : Visibility.Collapsed;
        Settings.Visibility = ReferenceEquals(target, Settings) ? Visibility.Visible : Visibility.Collapsed;
        Log.Visibility = ReferenceEquals(target, Log) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _refreshTimer.Stop(); _lifetime.Cancel();
        /* 需求1A：退出时自动保存当前设置（标定、映射、占空比记忆等），无需手动点保存。 */
        try
        {
            if (!_demo && _settings is not null)
            {
                if (_dashboard is not null)
                {
                    _settings.LastDutyPercent = _dashboard.ConfirmedDuty;
                    /* 关闭"上电默认回流阀"时启动恢复会继承这个开度，因此退出时记录当前确认值。 */
                    _settings.LastReturnOpeningPercent = _dashboard.ConfirmedReturnOpening;
                }

                bool saved = await _settings.SaveAsync(CancellationToken.None);
                if (saved)
                    _log.Add("退出时已自动保存本地配置。");
            }
        }
        catch (Exception exception)
        {
            _log.Add($"退出自动保存失败：{exception.Message}");
        }

        if (_session is not null) await _session.DisposeAsync();
        _lifetime.Dispose();
    }

    private static HostConfiguration CreateDemoConfiguration() => new(
        1, 30, 30,
        new ReturnValveCalibration(Enumerable.Range(1, 10).Select(x => new CalibrationPoint(x * 10, x * 1.2)).ToArray()),
        DateTimeOffset.UtcNow);
}

internal sealed class DisconnectedControllerSession : IControllerSession
{
    public bool IsConnected => false;
    public ControllerCapabilities Capabilities => new(1, 0);
    public ControllerParameterLimits ParameterLimits => ControllerParameterLimits.Default;
    public Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default) => Task.FromException(new IOException("控制器尚未连接。"));
    public Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default) => Task.FromException(new IOException("控制器尚未连接。"));
    public Task WriteOperatingSetpointsAsync(ushort duty, ushort targetCurrentMicroamps, CancellationToken cancellationToken = default) => Task.FromException(new IOException("控制器尚未连接。"));
    public Task SavePowerOnDefaultsAsync(NozzleMask mask, ushort duty, ushort targetCurrentMicroamps, CancellationToken cancellationToken = default) => Task.FromException(new IOException("控制器尚未连接。"));
    public Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default) => Task.FromException<ControllerStatus>(new IOException("控制器尚未连接。"));
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
