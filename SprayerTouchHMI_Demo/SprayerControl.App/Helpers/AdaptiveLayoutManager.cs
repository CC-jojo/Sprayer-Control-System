using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SprayerControl.App.Helpers;

/// <summary>
/// 上机自适应布局：程序启动后按当前设备实际分辨率、DPI 缩放与工作区大小，
/// 自动计算缩放比例，使 1920×1080 设计稿始终完整显示（不超出屏幕、不重叠），
/// 并让设计高度随屏幕宽高比变化以尽量减小留白。
/// </summary>
public static class AdaptiveLayoutManager
{
    private const double DesignWidth = 1920.0;   // 设计基准宽度
    private const double MinDesignHeight = 900.0;  // 超宽屏时的设计高度下限
    private const double MaxDesignHeight = 1350.0; // 超窄屏(如4:3)时的设计高度上限
    private const uint MonitorDefaultToNearest = 2;
    private const int MdTEffectiveDpi = 0;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>
    /// 在窗口 Loaded 之后调用一次。
    /// capture 为 true 时仅按 CapturePixelWidth/Height 缩放内容，不改动窗口（截图/导出用）。
    /// 窗口模式（--windowed）按窗口客户区大小缩放。
    /// 其余情况（默认全屏）将窗口铺满主显示器工作区（避开任务栏），再缩放内容。
    /// </summary>
    public static void Apply(Window window, FrameworkElement root, bool capture = false)
    {
        if (window is null || root is null)
        {
            return;
        }

        bool windowed = !capture && window.WindowStyle == WindowStyle.SingleBorderWindow;
        double availWidth;
        double availHeight;

        if (capture)
        {
            availWidth = App.CapturePixelWidth;
            availHeight = App.CapturePixelHeight;
        }
        else if (windowed)
        {
            var content = window.Content as FrameworkElement;
            availWidth = content is not null && content.ActualWidth > 10
                ? content.ActualWidth
                : Math.Max(window.ActualWidth - 16, 640);
            availHeight = content is not null && content.ActualHeight > 10
                ? content.ActualHeight
                : Math.Max(window.ActualHeight - 16, 480);
        }
        else
        {
            Rect area = GetWorkAreaDips(window);
            window.WindowState = WindowState.Normal;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = area.Left;
            window.Top = area.Top;
            window.Width = Math.Max(area.Width, 640);
            window.Height = Math.Max(area.Height, 480);
            availWidth = area.Width;
            availHeight = area.Height;
        }

        if (availWidth <= 0 || availHeight <= 0)
        {
            return;
        }

        // 根据屏幕宽高比确定设计高度：16:9 时 1080；16:10 时约 1200，从而填满屏幕避免大留白。
        double aspect = availWidth / Math.Max(availHeight, 1.0);
        double designHeight = Math.Clamp(DesignWidth / aspect, MinDesignHeight, MaxDesignHeight);

        // 统一缩放：取宽度与高度两个缩放中的较小值，保证内容既不超出屏幕也不重叠。
        double scale = Math.Min(availWidth / DesignWidth, availHeight / designHeight);

        root.Width = DesignWidth;
        root.Height = designHeight;
        root.LayoutTransform = new ScaleTransform(scale, scale);
    }

    /// <summary>获取窗口所在显示器的可用工作区（DIP 单位，考虑 DPI 缩放），失败时退回主屏工作区。</summary>
    private static Rect GetWorkAreaDips(Window window)
    {
        try
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                double k = 1.0;
                if (GetDpiForMonitor(monitor, MdTEffectiveDpi, out uint dpiX, out _) == 0 && dpiX > 0)
                {
                    k = 96.0 / dpiX;
                }

                return new Rect(
                    info.rcWork.Left * k,
                    info.rcWork.Top * k,
                    (info.rcWork.Right - info.rcWork.Left) * k,
                    (info.rcWork.Bottom - info.rcWork.Top) * k);
            }
        }
        catch
        {
            // 回退到主屏工作区
        }

        return SystemParameters.WorkArea;
    }
}
