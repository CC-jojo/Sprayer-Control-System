using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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

    public static readonly DependencyProperty IsPreviewingProperty = DependencyProperty.Register(
        nameof(IsPreviewing), typeof(bool), typeof(NozzleArrayControl), new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty IsPreStartProperty = DependencyProperty.Register(
        nameof(IsPreStart), typeof(bool), typeof(NozzleArrayControl), new PropertyMetadata(false, OnVisualStateChanged));

    /* 8 个喷头圆盘被点击时触发（group 1..4，right=true 为右侧）。 */
    public event Action<int, bool>? NozzleClicked;

    public NozzleArrayControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisualStates();
    }

    private void Nozzle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } button)
        {
            return;
        }

        string[] parts = tag.Split(',');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int group) &&
            int.TryParse(parts[1], out int side) &&
            side is 0 or 1)
        {
            NozzleClicked?.Invoke(group, side == 1);
        }
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

    public bool IsPreviewing
    {
        get => (bool)GetValue(IsPreviewingProperty);
        set => SetValue(IsPreviewingProperty, value);
    }

    public bool IsPreStart
    {
        get => (bool)GetValue(IsPreStartProperty);
        set => SetValue(IsPreStartProperty, value);
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
        ContentControl? indicator = FindName($"Nozzle{group}{sideName}Indicator") as ContentControl;
        if (indicator is null)
        {
            return;
        }

        int bit = (group - 1) * 2 + (right ? 1 : 0);
        byte displayedMask = IsPending || IsPreviewing || IsPreStart ? PreviewMask : ConfirmedMask;
        bool active = (displayedMask & (1 << bit)) != 0;
        string side = right ? "右侧" : "左侧";
        string state = active ? "开启" : "关闭";
        string status = IsPending ? "指令发送中" : IsPreStart ? "预启动待生效" : "已由控制器确认";
        // 圆盘既是状态指示器也是点击开关：
        // 预启动选中为青色，控制器确认开启为橙色，未选中为灰；点击直接切换。
        indicator.Foreground = FindBrush(active ? IsPreStart ? "HmiCyanBrush" : "HmiAmberBrush" : "HmiMutedTextBrush");
        indicator.BorderBrush = FindBrush(IsPending ? "HmiCyanBrush" : active ? (IsPreStart ? "HmiCyanBrush" : "HmiAmberBrush") : "HmiBorderBrush");
        AutomationProperties.SetName(indicator, $"第 {group} 组{side}喷嘴");
        AutomationProperties.SetHelpText(indicator, "点击切换喷头开关；总开关关闭时为预启动选中。");
        AutomationProperties.SetItemStatus(indicator, $"{state}；{status}");
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);
}
