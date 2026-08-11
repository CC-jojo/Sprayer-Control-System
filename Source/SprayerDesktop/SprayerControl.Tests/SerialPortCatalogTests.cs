using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class SerialPortCatalogTests
{
    [Fact]
    public async Task ReturnsBasePortsWhenFriendlyNameLookupTimesOut()
    {
        var neverCompletes = new TaskCompletionSource<IReadOnlyDictionary<string, string>>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        SerialPortDescriptor[] ports = await SerialPortCatalog.BuildAsync(
            ["COM12", "COM9"],
            _ => neverCompletes.Task,
            TimeSpan.FromMilliseconds(20),
            cancellation.Token);

        Assert.Equal(["COM9", "COM12"], ports.Select(port => port.PortName));
        Assert.All(ports, port => Assert.Equal("Serial port", port.FriendlyName));
    }

    [Fact]
    public async Task ReturnsBasePortsWhenFriendlyNameLookupThrows()
    {
        SerialPortDescriptor[] ports = await SerialPortCatalog.BuildAsync(
            ["COM12", "COM9"],
            _ => Task.FromException<IReadOnlyDictionary<string, string>>(
                new InvalidOperationException("WMI unavailable")),
            TimeSpan.FromMilliseconds(20));

        Assert.Equal(["COM9", "COM12"], ports.Select(port => port.PortName));
        Assert.All(ports, port => Assert.Equal("Serial port", port.FriendlyName));
    }
}
