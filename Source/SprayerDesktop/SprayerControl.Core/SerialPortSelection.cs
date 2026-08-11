namespace SprayerControl.Core;

public sealed record SerialPortDescriptor(string PortName, string FriendlyName)
{
    public bool IsRecommended => SerialPortSelection.GetRank(this) == 0;
    public string DisplayName => $"{PortName} — {FriendlyName}{(IsRecommended ? "（推荐）" : string.Empty)}";
}

public static class SerialPortSelection
{
    public static SerialPortDescriptor? ChoosePreferred(
        IEnumerable<SerialPortDescriptor> ports,
        string? previousPort = null)
    {
        SerialPortDescriptor[] available = ports.ToArray();
        SerialPortDescriptor? previous = available.FirstOrDefault(
            port => string.Equals(port.PortName, previousPort, StringComparison.OrdinalIgnoreCase));
        if (previous is not null && GetRank(previous) < 3) return previous;

        return available
            .OrderBy(GetRank)
            .ThenBy(port => ParsePortNumber(port.PortName))
            .FirstOrDefault();
    }

    public static int GetRank(SerialPortDescriptor port)
    {
        string name = port.FriendlyName;
        if (ContainsAny(name, "CH343", "CH340", "CH341")) return 0;
        if (ContainsAny(name, "USB Serial", "USB-Enhanced-SERIAL", "CP210", "FTDI")) return 1;
        if (ContainsAny(name, "STLink", "ST-Link", "STMicroelectronics")) return 3;
        if (ContainsAny(name, "Bluetooth", "蓝牙")) return 4;
        return 2;
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static int ParsePortNumber(string portName) =>
        int.TryParse(portName.AsSpan(3), out int number) ? number : int.MaxValue;
}
