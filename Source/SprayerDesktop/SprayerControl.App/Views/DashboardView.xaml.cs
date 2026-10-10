using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SprayerControl.App.ViewModels;
using SprayerControl.Core;

namespace SprayerControl.App.Views;
public partial class DashboardView : UserControl
{
    private DashboardViewModel? _viewModel;
    private bool _demo;
    public event Action? ShowLogRequested;
    public DashboardView() => InitializeComponent();
    public void Bind(DashboardViewModel viewModel, bool demo)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModelChanged;
        _viewModel = viewModel; _demo = demo; DataContext = viewModel; _viewModel.PropertyChanged += ViewModelChanged;
        Nozzles.NozzleClicked -= OnNozzleClicked;
        Nozzles.NozzleClicked += OnNozzleClicked;
        Nozzles.ConfirmedMask = viewModel.ConfirmedMask.Value; Nozzles.PreviewMask = viewModel.PreviewMask.Value;
        DutyGauge.Minimum = viewModel.PwmDutyMinimum; DutyGauge.Maximum = viewModel.PwmDutyMaximum; DutyGauge.ReleaseCommand = new DelegateCommand(value => { if (value is double duty) viewModel.PreviewDuty = (int)Math.Round(duty); _ = viewModel.CommitDutyAsync(); });
        DutyGauge.StepDownCommand = new DelegateCommand(_ => StepDuty(-1));
        DutyGauge.StepUpCommand = new DelegateCommand(_ => StepDuty(1));
        OpeningGauge.Minimum = 0; OpeningGauge.Maximum = 100; OpeningGauge.ReleaseCommand = new DelegateCommand(value => { if (value is double opening) viewModel.PreviewReturnOpening = opening; _ = viewModel.CommitReturnOpeningAsync(); });
        OpeningGauge.StepDownCommand = new DelegateCommand(_ => StepOpening(-1));
        OpeningGauge.StepUpCommand = new DelegateCommand(_ => StepOpening(1));
        // 只有回流阀仪表绑定"安全下限"一键按钮；占空比仪表未绑定，按钮自动隐藏。
        OpeningGauge.SafetyFloorCommand = new DelegateCommand(_ => ApplySafetyFloor());
        RecentLog.ItemsSource = viewModel.Log.RecentEntries; RefreshPresentation(demo);
    }
    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.Invoke(() => RefreshPresentation(_demo));
    private void RefreshPresentation(bool demo)
    {
        if (_viewModel is null) return;
        bool pwmEnabled = _viewModel.LatestStatus?.PwmEnabled == true;
        bool preStarting = !pwmEnabled;
        Nozzles.ConfirmedMask = _viewModel.ConfirmedMask.Value;
        Nozzles.PreviewMask = preStarting ? _viewModel.PreStartMask.Value : _viewModel.PreviewMask.Value;
        Nozzles.IsPending = _viewModel.IsNozzleCommandPending; Nozzles.IsPreviewing = false; Nozzles.IsPreStart = preStarting;
        // 提交期间不再禁用仪表/按钮或变暗进度条（并发安全已由操作门锁保证），
        // 避免点一个加减按钮导致两个仪表和总开关一起闪。
        DutyGauge.Value = _viewModel.ConfirmedDuty; DutyGauge.PreviewValue = _viewModel.PreviewDuty; DutyGauge.IsPending = false;
        OpeningGauge.Value = _viewModel.ConfirmedReturnOpening; OpeningGauge.PreviewValue = _viewModel.PreviewReturnOpening; OpeningGauge.SafetyMinimum = _viewModel.SafetyMinimum; OpeningGauge.IsPending = false;
        DutyGauge.Minimum = _viewModel.PwmDutyMinimum; DutyGauge.Maximum = _viewModel.PwmDutyMaximum;
        DutyGauge.IsEnabled = _viewModel.IsConnected && _viewModel.CanCommitSetpoints;
        OpeningGauge.IsEnabled = DutyGauge.IsEnabled;
        bool connected = _viewModel.IsConnected;
        bool pendingNozzles = _viewModel.IsNozzleCommandPending;
        Facts.Text = $"协议 v{_viewModel.Capabilities.ProtocolVersion}\n固件 {(_viewModel.LatestStatus is null ? "--" : $"0x{_viewModel.LatestStatus.FirmwareVersion:X4}")}";
        ConnectionTitle.Text = demo ? "演示控制器已连接" : connected ? "控制器已连接" : "正在查找控制器";
        ConnectionSubtext.Text = demo ? "样例数据不会保存" : connected ? "通信正常" : "自动发现与重连已启用";
        ConnectionStateDot.Stroke = FindBrush(connected ? "HmiGreenBrush" : "HmiAmberBrush");
        ConnectionStateDot.Fill = FindBrush(connected ? "HmiPanelBrush" : "HmiRaisedPanelBrush");
        ErrorText.Text = _viewModel.ErrorText;

        Nozzles.IsEnabled = connected && !pendingNozzles && _viewModel.Capabilities.SupportsExplicitNozzleMask;
        PwmMasterButton.IsEnabled = connected;
        bool preStartPending = _viewModel.PreStartMask.Value != 0;
        PwmStateText.Text = pwmEnabled ? "已开启" : preStartPending ? "已关闭 · 预启动待生效" : "已关闭";
        PwmStateText.Foreground = FindBrush(pwmEnabled ? "HmiGreenBrush" : preStartPending ? "HmiCyanBrush" : "HmiMutedTextBrush");
        SafetyMinimumText.Text = $"安全下限 {_viewModel.SafetyMinimum:F0}%";
        MappedCurrentText.Text = _viewModel.PreviewMappedCurrentMilliamps is double current ? $"映射电流 {current:F3} mA" : "映射电流 -- mA";
        UpdateAuthoritativeReadback(_viewModel.ConfirmedMask);
    }
    private void Pwm_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null && !_viewModel.IsSetpointCommandPending) _ = _viewModel.SetPwmEnabledAsync(_viewModel.LatestStatus?.PwmEnabled != true); }
    private void ShowLog_Click(object sender, RoutedEventArgs e) => ShowLogRequested?.Invoke();

    private void OnNozzleClicked(int group, bool right)
    {
        if (_viewModel is null || _viewModel.IsNozzleCommandPending) return;
        _ = _viewModel.ToggleNozzleAsync(group, right);
    }

    private void UpdateAuthoritativeReadback(NozzleMask mask)
    {
        Border[] cells = [State1L, State1R, State2L, State2R, State3L, State3R, State4L, State4R];
        for (int group = 1; group <= 4; group++)
        for (int side = 0; side < 2; side++)
        {
            bool active = mask.IsEnabled(group, right: side == 1);
            Border cell = cells[(group - 1) * 2 + side];
            cell.Margin = new Thickness(5, 3, 5, 3); cell.CornerRadius = new CornerRadius(5);
            cell.BorderThickness = new Thickness(1); cell.BorderBrush = FindBrush(active ? "HmiGreenBrush" : "HmiBorderBrush");
            cell.Background = active ? new SolidColorBrush(Color.FromRgb(17, 48, 34)) : FindBrush("HmiRaisedPanelBrush");
            cell.Child = new TextBlock { Text = active ? "●  开启" : "●  关闭", Foreground = FindBrush(active ? "HmiGreenBrush" : "HmiMutedTextBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
        }
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);

    private void StepDuty(int direction)
    {
        if (_viewModel is null || !_viewModel.CanCommitSetpoints || _viewModel.IsSetpointCommandPending)
        {
            return;
        }

        int target = (int)Math.Round(DutyGauge.PreviewValue) + direction;
        _viewModel.PreviewDuty = Math.Clamp(target, (int)DutyGauge.Minimum, (int)DutyGauge.Maximum);
        _ = _viewModel.CommitDutyAsync();
    }

    private void StepOpening(int direction)
    {
        if (_viewModel is null || !_viewModel.CanCommitSetpoints || _viewModel.IsSetpointCommandPending)
        {
            return;
        }

        double target = Math.Clamp(OpeningGauge.PreviewValue + direction, OpeningGauge.SafetyMinimum, OpeningGauge.Maximum);
        _viewModel.PreviewReturnOpening = target;
        _ = _viewModel.CommitReturnOpeningAsync();
    }

    /* 安全下限按钮：一键把回流阀开度切到安全下限。
       只有回流阀仪表绑定该命令，占空比仪表不绑定，按钮会自动隐藏。 */
    private void ApplySafetyFloor()
    {
        if (_viewModel is null || !_viewModel.CanCommitSetpoints || _viewModel.IsSetpointCommandPending)
        {
            return;
        }

        _ = _viewModel.ApplySafetyFloorAsync();
    }
}
internal sealed class DelegateCommand(Action<object?> execute) : System.Windows.Input.ICommand { public event EventHandler? CanExecuteChanged { add { } remove { } } public bool CanExecute(object? parameter) => true; public void Execute(object? parameter) => execute(parameter); }
