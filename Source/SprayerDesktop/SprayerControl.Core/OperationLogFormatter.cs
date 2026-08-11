namespace SprayerControl.Core;

public static class OperationLogFormatter
{
    public static string Format(DateTime timestamp, string message) =>
        $"[{timestamp:HH:mm:ss.fff}] {message}";
}
