using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SprayerControl.App.Controls;

public partial class SegmentedGaugeControl : UserControl
{
    private const int SegmentCount = 25;

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(SegmentedGaugeControl), new PropertyMetadata(0d, OnRangeChanged, CoerceMinimum));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(SegmentedGaugeControl), new PropertyMetadata(100d, OnRangeChanged, CoerceMaximum));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SegmentedGaugeControl), new PropertyMetadata(0d, OnRangeChanged, CoerceValue));

    public static readonly DependencyProperty PreviewValueProperty = DependencyProperty.Register(
        nameof(PreviewValue), typeof(double), typeof(SegmentedGaugeControl), new PropertyMetadata(0d, OnRangeChanged, CoercePreviewValue));

    public static readonly DependencyProperty SafetyMinimumProperty = DependencyProperty.Register(
        nameof(SafetyMinimum), typeof(double), typeof(SegmentedGaugeControl), new PropertyMetadata(0d, OnRangeChanged, CoerceValue));

    public static readonly DependencyProperty IsPendingProperty = DependencyProperty.Register(
        nameof(IsPending), typeof(bool), typeof(SegmentedGaugeControl), new PropertyMetadata(false, OnRangeChanged));

    public static readonly DependencyProperty ReleaseCommandProperty = DependencyProperty.Register(
        nameof(ReleaseCommand), typeof(ICommand), typeof(SegmentedGaugeControl));

    public static readonly DependencyProperty StepDownCommandProperty = DependencyProperty.Register(
        nameof(StepDownCommand), typeof(ICommand), typeof(SegmentedGaugeControl));

    public static readonly DependencyProperty StepUpCommandProperty = DependencyProperty.Register(
        nameof(StepUpCommand), typeof(ICommand), typeof(SegmentedGaugeControl));

    public SegmentedGaugeControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateSegments();
    }

    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public double PreviewValue { get => (double)GetValue(PreviewValueProperty); set => SetValue(PreviewValueProperty, value); }

    public double SafetyMinimum { get => (double)GetValue(SafetyMinimumProperty); set => SetValue(SafetyMinimumProperty, value); }

    public bool IsPending { get => (bool)GetValue(IsPendingProperty); set => SetValue(IsPendingProperty, value); }

    public ICommand? ReleaseCommand { get => (ICommand?)GetValue(ReleaseCommandProperty); set => SetValue(ReleaseCommandProperty, value); }

    public ICommand? StepDownCommand { get => (ICommand?)GetValue(StepDownCommandProperty); set => SetValue(StepDownCommandProperty, value); }

    public ICommand? StepUpCommand { get => (ICommand?)GetValue(StepUpCommandProperty); set => SetValue(StepUpCommandProperty, value); }

    private static object CoerceMinimum(DependencyObject dependencyObject, object baseValue)
    {
        double candidate = FiniteOr((double)baseValue, 0d);
        double maximum = ((SegmentedGaugeControl)dependencyObject).Maximum;
        return Math.Min(candidate, maximum);
    }

    private static object CoerceMaximum(DependencyObject dependencyObject, object baseValue)
    {
        double candidate = FiniteOr((double)baseValue, 100d);
        double minimum = ((SegmentedGaugeControl)dependencyObject).Minimum;
        return Math.Max(candidate, minimum);
    }

    private static object CoerceValue(DependencyObject dependencyObject, object baseValue)
    {
        SegmentedGaugeControl gauge = (SegmentedGaugeControl)dependencyObject;
        return Math.Clamp(FiniteOr((double)baseValue, gauge.Minimum), gauge.Minimum, gauge.Maximum);
    }

    private static object CoercePreviewValue(DependencyObject dependencyObject, object baseValue)
    {
        // 预览值只受量程约束；安全下限仅用于着色提示（橙色安全区），
        // 拖到安全区以下时让操作者看到真实位置，提交时再由上层钳制/提示。
        SegmentedGaugeControl gauge = (SegmentedGaugeControl)dependencyObject;
        return Math.Clamp(FiniteOr((double)baseValue, gauge.Minimum), gauge.Minimum, gauge.Maximum);
    }

    private static void OnRangeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs _)
    {
        SegmentedGaugeControl gauge = (SegmentedGaugeControl)dependencyObject;
        gauge.CoerceValue(SafetyMinimumProperty);
        gauge.CoerceValue(ValueProperty);
        gauge.CoerceValue(PreviewValueProperty);
        gauge.UpdateSegments();
    }

    private static double FiniteOr(double value, double fallback) => double.IsFinite(value) ? value : fallback;

    private void UpdateSegments()
    {
        if (!IsLoaded)
        {
            return;
        }

        double span = Math.Max(Maximum - Minimum, double.Epsilon);
        double activeRatio = (Value - Minimum) / span;
        double safetyRatio = (SafetyMinimum - Minimum) / span;
        for (int index = 1; index <= SegmentCount; index++)
        {
            if (FindName($"Segment{index:00}") is not Rectangle segment)
            {
                continue;
            }

            double ratio = (index - 0.5d) / SegmentCount;
            segment.Fill = FindBrush(ratio <= safetyRatio ? "HmiAmberBrush" : ratio <= activeRatio ? "HmiCyanBrush" : "HmiMutedTextBrush");
            segment.Opacity = IsPending ? 0.62d : 1d;
        }

        double selectableMinimum = Math.Max(Minimum, SafetyMinimum);
        AutomationProperties.SetName(this, "分段调节仪表");
        AutomationProperties.SetHelpText(this, $"范围 {Minimum:F0} 到 {Maximum:F0}；安全下限 {selectableMinimum:F0}；使用下方按钮步进调节。");
        AutomationProperties.SetItemStatus(this, IsPending
            ? $"等待控制器确认；预览 {PreviewValue:F0}"
            : $"当前 {Value:F0}；预览 {PreviewValue:F0}；安全下限 {selectableMinimum:F0}");
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);
}
