namespace SprayerControl.Core;

public static class SerialPortCatalog
{
    public static async Task<SerialPortDescriptor[]> BuildAsync(
        IEnumerable<string> portNames,
        Func<CancellationToken, Task<IReadOnlyDictionary<string, string>>> friendlyNameLookup,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        string[] available = portNames
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        IReadOnlyDictionary<string, string> friendlyNames;
        try
        {
            friendlyNames = await friendlyNameLookup(cancellationToken)
                .WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            friendlyNames = new Dictionary<string, string>();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            friendlyNames = new Dictionary<string, string>();
        }

        return available
            .Select(port => new SerialPortDescriptor(
                port,
                friendlyNames.TryGetValue(port, out string? name) ? name : "Serial port"))
            .OrderBy(SerialPortSelection.GetRank)
            .ThenBy(port => ParsePortNumber(port.PortName))
            .ToArray();
    }

    private static int ParsePortNumber(string portName) =>
        portName.Length > 3 && int.TryParse(portName.AsSpan(3), out int number)
            ? number
            : int.MaxValue;
}
