using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SprayerControl.App.ViewModels;
using SprayerControl.Core;

namespace SprayerControl.App.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;
    private int _editingOpening;
    private int _draftSafetyMinimum;
    private bool _safetyConfirmed = true;
    private bool _demo;
    private bool _controllerDefaultsArmed;
    private Func<CancellationToken, Task>? _saveControllerDefaults;

    public event Action? BackRequested;
    public event Action<HostConfiguration>? ConfigurationSaved;

    public SettingsView()
    {
        InitializeComponent();
        Keypad.ConfirmCommand = new DelegateCommand(ConfirmKeypad);
        Keypad.CancelCommand = new DelegateCommand(_ => Keypad.Visibility = Visibility.Collapsed);
    }

    public void Bind(SettingsViewModel viewModel, bool demo)
    {
        _viewModel = viewModel; _demo = demo; DataContext = viewModel;
        _draftSafetyMinimum = viewModel.ReturnValveSafetyMinimum; _safetyConfirmed = true;
        SaveLocalButton.IsEnabled = viewModel.CanSaveLocal;
        Refresh();
        ResultText.Text = demo ? "演示模式：本地配置与控制器上电默认值均禁止写入。" : string.Empty;
    }

    public void ConfigureControllerDefaults(bool available, string reason, Func<CancellationToken, Task>? action)
    {
        _controllerDefaultsArmed = false; _saveControllerDefaults = action;
        SaveControllerDefaultButton.Content = "保存为上电默认值";
        SaveControllerDefaultButton.IsEnabled = available;
        ControllerDefaultReason.Text = available ? "需连续两次确认；写入当前控制器确认值。" : reason;
    }

    private void Refresh()
    {
        if (_viewModel is null) return;
        PwmValue.Text = $"{_viewModel.PwmDutyMinimum}%（上限固定 80%）";
        SafetyValue.Text = $"{_draftSafetyMinimum}%{(_safetyConfirmed ? string.Empty : "（待确认）")}";
        ConfirmSafetyButton.IsEnabled = !_safetyConfirmed;
        CalibrationGrid.Children.Clear();
        for (int opening = 10; opening <= 100; opening += 10)
        {
            var button = new Button
            {
                Content = $"{opening}%    {(_viewModel.CalibrationCurrents[(opening / 10) - 1]?.ToString("F3", CultureInfo.InvariantCulture) ?? "未标定")} mA",
                Tag = opening, Style = (Style)FindResource("TouchButtonStyle"), Margin = new Thickness(4), FontSize = 15
            };
            button.Click += EditCalibration_Click; CalibrationGrid.Children.Add(button);
        }
        DrawCurve();
    }

    private void DrawCurve()
    {
        CurveLine.Points.Clear();
        IReadOnlyList<CalibrationPoint> points = _viewModel?.ValidatedCurvePoints ?? [];
        if (points.Count != 10) { MappedCurrentText.Text = "安全下限映射电流：完成十点标定后显示"; return; }
        double min = points.Min(point => point.CurrentMilliamps), max = points.Max(point => point.CurrentMilliamps);
        double span = Math.Max(max - min, 0.001);
        foreach (CalibrationPoint point in points)
            CurveLine.Points.Add(new Point(36 + (point.OpeningPercent - 10) / 90d * 286, 218 - (point.CurrentMilliamps - min) / span * 190));
        try
        {
            var curve = new ReturnValveCalibration(points);
            MappedCurrentText.Text = $"安全下限映射电流：{curve.CurrentMilliampsAt(_draftSafetyMinimum):F3} mA";
        }
        catch { MappedCurrentText.Text = "安全下限映射电流：-- mA"; }
    }

    private void EditCalibration_Click(object sender, RoutedEventArgs e) { _editingOpening = (int)((Button)sender).Tag; Keypad.Text = _viewModel!.CalibrationCurrents[(_editingOpening / 10) - 1]?.ToString(CultureInfo.InvariantCulture) ?? string.Empty; Keypad.Visibility = Visibility.Visible; }
    private void ConfirmKeypad(object? value) { if (double.TryParse(value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out double current)) { _viewModel!.SetCalibrationCurrent(_editingOpening, current); Keypad.Visibility = Visibility.Collapsed; Refresh(); } else ResultText.Text = "请输入有效的毫安数值。"; }
    private void DecreasePwm_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null) { _viewModel.PwmDutyMinimum = Math.Max(30, _viewModel.PwmDutyMinimum - 1); Refresh(); } }
    private void IncreasePwm_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null) { _viewModel.PwmDutyMinimum = Math.Min(80, _viewModel.PwmDutyMinimum + 1); Refresh(); } }
    private void DecreaseSafety_Click(object sender, RoutedEventArgs e) { _draftSafetyMinimum = Math.Max(10, _draftSafetyMinimum - 1); _safetyConfirmed = _viewModel is not null && _draftSafetyMinimum == _viewModel.ReturnValveSafetyMinimum; Refresh(); }
    private void IncreaseSafety_Click(object sender, RoutedEventArgs e) { _draftSafetyMinimum = Math.Min(100, _draftSafetyMinimum + 1); _safetyConfirmed = _viewModel is not null && _draftSafetyMinimum == _viewModel.ReturnValveSafetyMinimum; Refresh(); }
    private void ConfirmSafety_Click(object sender, RoutedEventArgs e) { if (_viewModel is null) return; int oldValue = _viewModel.ReturnValveSafetyMinimum; _viewModel.ReturnValveSafetyMinimum = _draftSafetyMinimum; _safetyConfirmed = true; ResultText.Text = $"已确认安全下限修改：{oldValue}% → {_draftSafetyMinimum}%；触摸保存后生效。"; Refresh(); }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        if (!_safetyConfirmed) { ResultText.Text = "请先触摸“确认安全下限修改”。"; return; }
        bool saved = await _viewModel.SaveAsync(); ResultText.Text = saved ? "本地配置已验证并保存。" : _viewModel.ValidationError;
        if (saved && _viewModel.SavedConfiguration is not null) ConfigurationSaved?.Invoke(_viewModel.SavedConfiguration);
        Refresh();
    }

    private async void SaveControllerDefault_Click(object sender, RoutedEventArgs e)
    {
        if (_demo || _saveControllerDefaults is null) return;
        if (!_controllerDefaultsArmed)
        {
            _controllerDefaultsArmed = true; SaveControllerDefaultButton.Content = "再次触摸确认写入";
            ControllerDefaultReason.Text = "将写入当前喷嘴、占空比及回流阀电流；再次触摸才执行。"; return;
        }
        SaveControllerDefaultButton.IsEnabled = false;
        try { await _saveControllerDefaults(CancellationToken.None); ResultText.Text = "控制器上电默认值已写入并确认。"; }
        catch (Exception exception) { ResultText.Text = $"写入失败：{exception.Message}"; }
        finally { _controllerDefaultsArmed = false; SaveControllerDefaultButton.Content = "保存为上电默认值"; SaveControllerDefaultButton.IsEnabled = true; }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();
}
