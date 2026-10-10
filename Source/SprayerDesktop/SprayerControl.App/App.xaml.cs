using System.Configuration;
using System.Data;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace SprayerControl.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static bool DemoMode { get; private set; }
    public static string? CapturePath { get; private set; }
    public static int CapturePixelWidth { get; private set; }
    public static int CapturePixelHeight { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Demo 版发布时 AssemblyName 为 SprayerTouchHMI_Demo，即使 exe 被重命名也能自动进入演示模式。
        DemoMode = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase) || IsDemoAssembly();
        bool windowed = e.Args.Contains("--windowed", StringComparer.OrdinalIgnoreCase);
        int captureIndex = Array.FindIndex(e.Args, value => string.Equals(value, "--capture", StringComparison.OrdinalIgnoreCase));
        bool capture = captureIndex >= 0;
        CapturePath = capture && captureIndex + 1 < e.Args.Length && !e.Args[captureIndex + 1].StartsWith("--", StringComparison.Ordinal)
            ? Path.GetFullPath(e.Args[captureIndex + 1]) : null;
        if (capture && CapturePath is null) throw new ArgumentException("--capture requires an output PNG path.");
        if (windowed || capture)
        {
            CapturePixelWidth = (int)Math.Round(ReadDimension(e.Args, "--width", 1366));
            CapturePixelHeight = (int)Math.Round(ReadDimension(e.Args, "--height", 768));
        }
        base.OnStartup(e);
        if (MainWindow is MainWindow window && (windowed || capture))
        {
            window.WindowStyle = capture ? WindowStyle.None : WindowStyle.SingleBorderWindow;
            window.WindowState = WindowState.Normal;
            window.Width = CapturePixelWidth;
            window.Height = CapturePixelHeight;
        }
    }

    internal static void Capture(Window window)
    {
        try
        {
            int width = CapturePixelWidth > 0 ? CapturePixelWidth : checked((int)Math.Round(window.ActualWidth));
            int height = CapturePixelHeight > 0 ? CapturePixelHeight : checked((int)Math.Round(window.ActualHeight));
            if (width < 1 || height < 1) throw new InvalidOperationException("截图窗口尚未完成测量。");
            if (window.Content is not FrameworkElement content) throw new InvalidOperationException("截图内容不可用。");
            window.WindowState = WindowState.Normal; window.Width = width; window.Height = height;
            window.Left = -20000; window.Top = -20000;
            window.UpdateLayout();
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(CapturePath!)!);
            using var stream = File.Create(CapturePath!); encoder.Save(stream);
        }
        catch { Environment.ExitCode = 1; throw; }
        finally { Current.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Send); }
    }

    private static bool IsDemoAssembly() =>
        string.Equals(Assembly.GetExecutingAssembly().GetName().Name, "SprayerTouchHMI_Demo", StringComparison.OrdinalIgnoreCase);

    private static double ReadDimension(string[] args, string name, double fallback)
    {
        int index = Array.FindIndex(args, value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && double.TryParse(args[index + 1], out double value) && value >= 640 ? value : fallback;
    }
}

