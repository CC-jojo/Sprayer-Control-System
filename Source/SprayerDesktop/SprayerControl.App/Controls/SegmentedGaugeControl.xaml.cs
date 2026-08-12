using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SprayerControl.App.Controls;

public partial class SegmentedGaugeControl : UserControl
{
    private const int SegmentCount = 20;
    private bool _gestureActive;
    private bool _gestureCancelled;

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
        SegmentedGaugeControl gauge = (SegmentedGaugeControl)dependencyObject;
        double selectableMinimum = Math.Min(gauge.Maximum, Math.Max(gauge.Minimum, gauge.SafetyMinimum));
        return Math.Clamp(FiniteOr((double)baseValue, selectableMinimum), selectableMinimum, gauge.Maximum);
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

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.StylusDevice is not null || IsPending)
        {
            return;
        }

        BeginGesture(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_gestureActive && e.LeftButton == MouseButtonState.Pressed)
        {
            UpdatePreviewFrom(e.GetPosition(this));
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.StylusDevice is null)
        {
            if (IsPending)
            {
                CancelGesture();
            }
            else
            {
                CompleteGesture();
            }
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e) => CancelGesture();

    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        if (IsPending)
        {
            return;
        }

        BeginGesture(e.GetTouchPoint(this).Position);
        e.TouchDevice.Capture(this);
        e.Handled = true;
    }

    private void OnTouchMove(object sender, TouchEventArgs e)
    {
        if (_gestureActive)
        {
            UpdatePreviewFrom(e.GetTouchPoint(this).Position);
            e.Handled = true;
        }
    }

    private void OnTouchUp(object sender, TouchEventArgs e)
    {
        if (IsPending)
        {
            CancelGesture();
        }
        else
        {
            CompleteGesture();
        }
        e.TouchDevice.Capture(null);
        e.Handled = true;
    }

    private void OnLostTouchCapture(object sender, TouchEventArgs e) => CancelGesture();

    private void BeginGesture(Point position)
    {
        if (IsPending)
        {
            return;
        }

        _gestureActive = true;
        _gestureCancelled = false;
        UpdatePreviewFrom(position);
    }

    private void UpdatePreviewFrom(Point position)
    {
        if (IsPending || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double x = position.X - ActualWidth / 2d;
        double y = position.Y - ActualHeight / 2d;
        double angle = Math.Atan2(x, -y);
        if (angle < 0)
        {
            angle += Math.PI * 2d;
        }

        PreviewValue = Minimum + (angle / (Math.PI * 2d) * (Maximum - Minimum));
    }

    private void CompleteGesture()
    {
        if (IsPending || !_gestureActive)
        {
            return;
        }

        _gestureActive = false;
        if (!_gestureCancelled && ReleaseCommand?.CanExecute(PreviewValue) == true)
        {
            ReleaseCommand.Execute(PreviewValue);
        }
    }

    private void CancelGesture()
    {
        _gestureCancelled = true;
        _gestureActive = false;
    }

    private void UpdateSegments()
    {
        if (!IsLoaded)
        {
            return;
        }

        double span = Math.Max(Maximum - Minimum, double.Epsilon);
        double activeRatio = ((_gestureActive ? PreviewValue : Value) - Minimum) / span;
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
        AutomationProperties.SetHelpText(this, $"范围 {Minimum:F0} 到 {Maximum:F0}；安全下限 {selectableMinimum:F0}；拖动后松开确认。");
        AutomationProperties.SetItemStatus(this, IsPending
            ? $"等待控制器确认；预览 {PreviewValue:F0}"
            : $"当前 {Value:F0}；预览 {PreviewValue:F0}；安全下限 {selectableMinimum:F0}");
    }

    private Brush FindBrush(string key) => (Brush)FindResource(key);
}
