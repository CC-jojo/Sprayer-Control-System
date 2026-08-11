using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
using SprayerControl.Core;

namespace SprayerControl.App;

internal static partial class WindowsSerialPortDiscovery
{
    public static Task<SerialPortDescriptor[]> GetPortsAsync(
        CancellationToken cancellationToken = default)
    {
        string[] portNames = SerialPort.GetPortNames();
        return SerialPortCatalog.BuildAsync(
            portNames,
            _ => Task.Run<IReadOnlyDictionary<string, string>>(ReadFriendlyNames),
            TimeSpan.FromMilliseconds(500),
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> ReadFriendlyNames()
    {
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
        foreach (ManagementObject device in searcher.Get())
        {
            string friendlyName = device["Name"]?.ToString() ?? string.Empty;
            Match match = ComPortRegex().Match(friendlyName);
            if (match.Success)
                names[match.Groups[1].Value] = friendlyName[..match.Index].Trim();
        }

        return names;
    }

    [GeneratedRegex(@"\((COM\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortRegex();
}
