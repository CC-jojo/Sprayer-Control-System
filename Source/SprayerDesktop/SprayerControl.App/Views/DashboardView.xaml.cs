using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SprayerControl.App.Controls;
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
        Nozzles.ConfirmedMask = viewModel.ConfirmedMask.Value; Nozzles.PreviewMask = viewModel.PreviewMask.Value;
        Nozzles.Command = new GuardedDelegateCommand(p => Toggle((NozzleCommandParameter)p!), () => _viewModel?.IsNozzleCommandPending != true);
        DutyGauge.Minimum = viewModel.PwmDutyMinimum; DutyGauge.Maximum = 80; DutyGauge.ReleaseCommand = new DelegateCommand(value => { if (value is double duty) viewModel.PreviewDuty = (int)Math.Round(duty); _ = viewModel.CommitDutyAsync(); });
        OpeningGauge.Minimum = 0; OpeningGauge.Maximum = 100; OpeningGauge.ReleaseCommand = new DelegateCommand(value => { if (value is double opening) viewModel.PreviewReturnOpening = opening; _ = viewModel.CommitReturnOpeningAsync(); });
        RecentLog.ItemsSource = viewModel.Log.RecentEntries; RefreshPresentation(demo);
    }
    private void ViewModelChanged(object? sender, PropertyChangedEventArgs e) => Dispatcher.Invoke(() => RefreshPresentation(_demo));
    private void RefreshPresentation(bool demo)
    {
        if (_viewModel is null) return;
        Nozzles.ConfirmedMask = _viewModel.ConfirmedMask.Value; Nozzles.PreviewMask = _viewModel.PreviewMask.Value; Nozzles.IsPending = _viewModel.IsNozzleCommandPending;
        DutyGauge.Value = _viewModel.ConfirmedDuty; DutyGauge.PreviewValue = _viewModel.PreviewDuty; DutyGauge.IsPending = _viewModel.IsSetpointCommandPending;
        OpeningGauge.Value = _viewModel.ConfirmedReturnOpening; OpeningGauge.PreviewValue = _viewModel.PreviewReturnOpening; OpeningGauge.SafetyMinimum = _viewModel.SafetyMinimum; OpeningGauge.IsPending = _viewModel.IsSetpointCommandPending;
        DutyGauge.Minimum = _viewModel.PwmDutyMinimum;
        DutyGauge.IsEnabled = _viewModel.IsConnected && _viewModel.CanCommitSetpoints && !_viewModel.IsSetpointCommandPending;
        OpeningGauge.IsEnabled = DutyGauge.IsEnabled;
        bool connected = _viewModel.IsConnected;
        bool pendingNozzles = _viewModel.IsNozzleCommandPending;
        bool pendingSetpoints = _viewModel.IsSetpointCommandPending;
        bool pwmEnabled = _viewModel.LatestStatus?.PwmEnabled == true;
        Facts.Text = $"协议 v{_viewModel.Capabilities.ProtocolVersion}\n固件 {(_viewModel.LatestStatus is null ? "--" : $"0x{_viewModel.LatestStatus.FirmwareVersion:X4}")}";
        ConnectionTitle.Text = demo ? "演示控制器已连接" : connected ? "控制器已连接" : "正在查找控制器";
        ConnectionSubtext.Text = demo ? "样例数据不会保存" : connected ? "通信正常" : "自动发现与重连已启用";
        ConnectionStateDot.Stroke = FindBrush(connected ? "HmiGreenBrush" : "HmiAmberBrush");
        ConnectionStateDot.Fill = FindBrush(connected ? "HmiPanelBrush" : "HmiRaisedPanelBrush");
        ErrorText.Text = _viewModel.ErrorText;

        LeftMasterButton.IsEnabled = connected && !pendingNozzles && _viewModel.Capabilities.SupportsExplicitNozzleMask;
        RightMasterButton.IsEnabled = LeftMasterButton.IsEnabled;
        PresetAllButton.IsEnabled = LeftMasterButton.IsEnabled; Preset234Button.IsEnabled = LeftMasterButton.IsEnabled;
        Preset34Button.IsEnabled = LeftMasterButton.IsEnabled; Preset4Button.IsEnabled = LeftMasterButton.IsEnabled;
        PwmMasterButton.IsEnabled = connected && !pendingSetpoints;
        LeftMasterState.Text = pendingNozzles ? "等待确认" : _viewModel.LeftEnabled ? "已开启" : "已关闭";
        RightMasterState.Text = pendingNozzles ? "等待确认" : _viewModel.RightEnabled ? "已开启" : "已关闭";
        LeftMasterState.Foreground = FindBrush(_viewModel.LeftEnabled ? "HmiGreenBrush" : "HmiMutedTextBrush");
        RightMasterState.Foreground = FindBrush(_viewModel.RightEnabled ? "HmiGreenBrush" : "HmiMutedTextBrush");
        PwmStateText.Text = pendingSetpoints ? "等待确认" : pwmEnabled ? "已开启" : "已关闭";
        PwmStateText.Foreground = FindBrush(pwmEnabled ? "HmiGreenBrush" : "HmiMutedTextBrush");
        SafetyMinimumText.Text = $"安全下限 {_viewModel.SafetyMinimum:F0}%";
        MappedCurrentText.Text = _viewModel.PreviewMappedCurrentMilliamps is double current ? $"映射电流 {current:F3} mA" : "映射电流 -- mA";
        UpdateAuthoritativeReadback(_viewModel.ConfirmedMask);
        UpdatePresetSelection(_viewModel.ConfirmedMask);
    }
    private void Toggle(NozzleCommandParameter parameter) { if (_viewModel is not null) _ = _viewModel.ToggleNozzleAsync(parameter.Group, parameter.Right); }
    private void LeftMaster_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null && !_viewModel.IsNozzleCommandPending) _ = _viewModel.SetLeftMasterAsync(!_viewModel.LeftEnabled); }
    private void RightMaster_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null && !_viewModel.IsNozzleCommandPending) _ = _viewModel.SetRightMasterAsync(!_viewModel.RightEnabled); }
    private void Pwm_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null && !_viewModel.IsSetpointCommandPending) _ = _viewModel.SetPwmEnabledAsync(_viewModel.LatestStatus?.PwmEnabled != true); }
    private void Preset_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null && !_viewModel.IsNozzleCommandPending) _ = _viewModel.ApplyCanopyPresetAsync(((Button)sender).Tag?.ToString() switch { "4" => CanopyPreset.Group4, "34" => CanopyPreset.Groups34, "234" => CanopyPreset.Groups234, _ => CanopyPreset.AllGroups }); }
    private void ShowLog_Click(object sender, RoutedEventArgs e) => ShowLogRequested?.Invoke();

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

    private void UpdatePresetSelection(NozzleMask mask)
    {
        byte groupBits = 0;
        for (int group = 1; group <= 4; group++) if (mask.IsEnabled(group, false) || mask.IsEnabled(group, true)) groupBits |= (byte)(1 << (group - 1));
        Button? selected = groupBits switch { 0x0F => PresetAllButton, 0x0E => Preset234Button, 0x0C => Preset34Button, 0x08 => Preset4Button, _ => null };
        foreach (Button button in new[] { PresetAllButton, Preset234Button, Preset34Button, Preset4Button })
        {
            button.BorderBrush = FindBrush(ReferenceEquals(button, selected) ? "HmiCyanBrush" : "HmiBorderBrush");
            button.Background = ReferenceEquals(button, selected) ? new SolidColorBrush(Color.FromRgb(16, 48, 75)) : FindBrush("HmiRaisedPanelBrush");
        }
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);
}
internal sealed class DelegateCommand(Action<object?> execute) : System.Windows.Input.ICommand { public event EventHandler? CanExecuteChanged { add { } remove { } } public bool CanExecute(object? parameter) => true; public void Execute(object? parameter) => execute(parameter); }
internal sealed class GuardedDelegateCommand(Action<object?> execute, Func<bool> canExecute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => canExecute();
    public void Execute(object? parameter) { if (canExecute()) execute(parameter); }
}
