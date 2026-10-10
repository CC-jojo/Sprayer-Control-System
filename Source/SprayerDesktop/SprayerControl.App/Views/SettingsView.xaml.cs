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
        RefreshMappingGrid();
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
        PwmValue.Text = $"{_viewModel.PwmDutyMinimum}%（上限 {_viewModel.PwmDutyMaximum}%）";
        SafetyValue.Text = $"{_draftSafetyMinimum}%{(_safetyConfirmed ? string.Empty : "（待确认）")}";
        ConfirmSafetyButton.IsEnabled = !_safetyConfirmed;
        PowerOnOpeningValue.Text = $"{_viewModel.PowerOnReturnValvePercent}%";
        PowerOnToggleButton.Content = _viewModel.PowerOnReturnValveEnabled ? "上电用此开度" : "继承上次开度";
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
    private async void ConfirmKeypad(object? value)
    {
        if (double.TryParse(value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out double current))
        {
            _viewModel!.SetCalibrationCurrent(_editingOpening, current);
            Keypad.Visibility = Visibility.Collapsed;
            Refresh();
            /* 十点标定填满后自动保存（非演示模式），避免只标定不点保存、重启后丢失。 */
            if (!_demo && _viewModel.IsCalibrationComplete)
                await SaveAndApplyAsync(autoSaved: true);
        }
        else ResultText.Text = "请输入有效的毫安数值。";
    }
    private void DecreasePwm_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null) { _viewModel.PwmDutyMinimum = Math.Max(30, _viewModel.PwmDutyMinimum - 1); Refresh(); } }
    private void IncreasePwm_Click(object sender, RoutedEventArgs e) { if (_viewModel is not null) { _viewModel.PwmDutyMinimum = Math.Min(_viewModel.PwmDutyMaximum, _viewModel.PwmDutyMinimum + 1); Refresh(); } }
    private void DecreaseSafety_Click(object sender, RoutedEventArgs e) { _draftSafetyMinimum = Math.Max(10, _draftSafetyMinimum - 1); _safetyConfirmed = _viewModel is not null && _draftSafetyMinimum == _viewModel.ReturnValveSafetyMinimum; Refresh(); }
    private void IncreaseSafety_Click(object sender, RoutedEventArgs e) { _draftSafetyMinimum = Math.Min(100, _draftSafetyMinimum + 1); _safetyConfirmed = _viewModel is not null && _draftSafetyMinimum == _viewModel.ReturnValveSafetyMinimum; Refresh(); }
    private void ConfirmSafety_Click(object sender, RoutedEventArgs e) { if (_viewModel is null) return; int oldValue = _viewModel.ReturnValveSafetyMinimum; _viewModel.ReturnValveSafetyMinimum = _draftSafetyMinimum; _safetyConfirmed = true; ResultText.Text = $"已确认安全下限修改：{oldValue}% → {_draftSafetyMinimum}%；触摸保存后生效。"; Refresh(); }

    /* 上电默认回流阀：加减每按一次步进 1%，并钳制在安全下限与 100% 之间。 */
    private void DecreasePowerOn_Click(object sender, RoutedEventArgs e) => AdjustPowerOnOpening(-1);
    private void IncreasePowerOn_Click(object sender, RoutedEventArgs e) => AdjustPowerOnOpening(1);

    private void AdjustPowerOnOpening(int delta)
    {
        if (_viewModel is null) return;
        int floor = Math.Max(_viewModel.ReturnValveSafetyMinimum, _draftSafetyMinimum);
        _viewModel.PowerOnReturnValvePercent = Math.Clamp(_viewModel.PowerOnReturnValvePercent + delta, floor, 100);
        Refresh();
    }

    private void TogglePowerOn_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        _viewModel.PowerOnReturnValveEnabled = !_viewModel.PowerOnReturnValveEnabled;
        ResultText.Text = _viewModel.PowerOnReturnValveEnabled
            ? $"已打开上电默认回流阀：上电与打开上位机都会把回流阀调到 {_viewModel.PowerOnReturnValvePercent}%；触摸保存后生效。"
            : "已关闭上电默认回流阀：上电与打开上位机将继承上次回流阀开度；触摸保存后生效。";
        Refresh();
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAndApplyAsync();

    /* 统一的"保存本地配置并应用到仪表盘"入口；autoSaved=true 用于十点填满后的自动保存提示。 */
    private async Task<bool> SaveAndApplyAsync(bool autoSaved = false)
    {
        if (_viewModel is null) return false;
        if (!_safetyConfirmed) { ResultText.Text = "请先触摸“确认安全下限修改”。"; return false; }
        bool saved = await _viewModel.SaveAsync();
        ResultText.Text = saved
            ? autoSaved ? "十点标定已填满，本地配置已自动保存。" : "本地配置已验证并保存。"
            : _viewModel.ValidationError;
        if (saved && _viewModel.SavedConfiguration is not null) ConfigurationSaved?.Invoke(_viewModel.SavedConfiguration);
        Refresh();
        return saved;
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

    private void RefreshMappingGrid()
    {
        MappingGrid.Children.Clear();
        if (_viewModel is null) return;
        for (int index = 0; index < _viewModel.NozzleMappingEntries.Count; index++)
        {
            NozzleMappingEntry entry = _viewModel.NozzleMappingEntries[index];
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = entry.UiLabel,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("HmiTextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Width = 46,
            };
            var combo = new ComboBox { Width = 125, FontSize = 18, Tag = index };
            foreach (string physicalLabel in NozzleMappingEntry.PhysicalLabels)
                combo.Items.Add(physicalLabel);
            combo.SelectedIndex = entry.SelectedPhysicalIndex;
            combo.SelectionChanged += Mapping_SelectionChanged;
            panel.Children.Add(label);
            panel.Children.Add(combo);
            MappingGrid.Children.Add(panel);
        }
    }

    private void Mapping_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: int index } combo && combo.SelectedIndex >= 0)
            _viewModel?.SetNozzleMappingSelection(index, combo.SelectedIndex);
    }

    private void ResetMapping_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.ResetNozzleMapping();
        RefreshMappingGrid();
        ResultText.Text = "已恢复默认八路映射；触摸“应用并保存本地”生效。";
    }
}
