using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SprayerControl.App.Controls;

public partial class NozzleArrayControl : UserControl
{
    public static readonly DependencyProperty ConfirmedMaskProperty = DependencyProperty.Register(
        nameof(ConfirmedMask), typeof(byte), typeof(NozzleArrayControl), new PropertyMetadata((byte)0, OnVisualStateChanged));

    public static readonly DependencyProperty PreviewMaskProperty = DependencyProperty.Register(
        nameof(PreviewMask), typeof(byte), typeof(NozzleArrayControl), new PropertyMetadata((byte)0, OnVisualStateChanged));

    public static readonly DependencyProperty IsPendingProperty = DependencyProperty.Register(
        nameof(IsPending), typeof(bool), typeof(NozzleArrayControl), new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(NozzleArrayControl));

    public NozzleArrayControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisualStates();
    }

    public byte ConfirmedMask
    {
        get => (byte)GetValue(ConfirmedMaskProperty);
        set => SetValue(ConfirmedMaskProperty, value);
    }

    public byte PreviewMask
    {
        get => (byte)GetValue(PreviewMaskProperty);
        set => SetValue(PreviewMaskProperty, value);
    }

    public bool IsPending
    {
        get => (bool)GetValue(IsPendingProperty);
        set => SetValue(IsPendingProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private static void OnVisualStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _) =>
        ((NozzleArrayControl)dependencyObject).UpdateVisualStates();

    private void UpdateVisualStates()
    {
        if (!IsLoaded)
        {
            return;
        }

        for (int group = 1; group <= 4; group++)
        {
            UpdateNozzle(group, right: false);
            UpdateNozzle(group, right: true);
        }
    }

    private void UpdateNozzle(int group, bool right)
    {
        string sideName = right ? "Right" : "Left";
        Button? button = FindName($"Nozzle{group}{sideName}Button") as Button;
        if (button is null)
        {
            return;
        }

        int bit = (group - 1) * 2 + (right ? 1 : 0);
        byte displayedMask = IsPending ? PreviewMask : ConfirmedMask;
        bool active = (displayedMask & (1 << bit)) != 0;
        string side = right ? "右侧" : "左侧";
        string state = active ? "开启" : "关闭";
        string pending = IsPending ? "指令发送中" : "已由控制器确认";
        button.IsEnabled = !IsPending;
        button.Background = FindBrush(active ? "HmiAmberBrush" : "HmiMutedTextBrush");
        button.Foreground = FindBrush("HmiRaisedPanelBrush");
        button.BorderBrush = FindBrush(IsPending ? "HmiCyanBrush" : active ? "HmiAmberBrush" : "HmiBorderBrush");
        AutomationProperties.SetName(button, $"第 {group} 组{side}喷嘴");
        AutomationProperties.SetHelpText(button, "触摸可切换喷嘴；控制器读回状态为准。");
        AutomationProperties.SetItemStatus(button, $"{state}；{pending}");
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);
}

public sealed class NozzleCommandParameter
{
    public int Group { get; set; }

    public bool Right { get; set; }
}
