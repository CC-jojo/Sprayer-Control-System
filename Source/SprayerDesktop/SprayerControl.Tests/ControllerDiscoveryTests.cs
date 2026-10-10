using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ControllerDiscoveryTests
{
    [Fact]
    public async Task PreferredPortIsProbedFirstWhenPresent()
    {
        var probed = new List<string>();
        SerialPortDescriptor[] ports =
        [
            new("COM3", "USB Serial Device"),
            new("COM9", "USB-Enhanced-SERIAL CH343"),
        ];

        ControllerConnection? connection = await ControllerDiscovery.TryDiscoverAsync(
            ports,
            port => new ProbeChannel(port, probed),
            preferredPortName: "COM3");

        Assert.NotNull(connection);
        Assert.Equal("COM3", connection.Port.PortName);
        Assert.Equal(["COM3"], probed);
    }

    [Fact]
    public async Task MissingPreferredPortFallsBackToRankedScan()
    {
        var probed = new List<string>();
        SerialPortDescriptor[] ports =
        [
            new("COM3", "USB Serial Device"),
            new("COM9", "USB-Enhanced-SERIAL CH343"),
        ];

        ControllerConnection? connection = await ControllerDiscovery.TryDiscoverAsync(
            ports,
            port => new ProbeChannel(port, probed),
            preferredPortName: "COM99");

        Assert.NotNull(connection);
        Assert.Equal("COM9", connection.Port.PortName);
        Assert.Equal(["COM9"], probed);
    }

    [Fact]
    public async Task DefaultScanOrderIsUnchangedWithoutPreferredPort()
    {
        var probed = new List<string>();
        SerialPortDescriptor[] ports =
        [
            new("COM3", "USB Serial Device"),
            new("COM9", "USB-Enhanced-SERIAL CH343"),
        ];

        ControllerConnection? connection = await ControllerDiscovery.TryDiscoverAsync(
            ports,
            port => new ProbeChannel(port, probed));

        Assert.NotNull(connection);
        Assert.Equal("COM9", connection.Port.PortName);
        Assert.Equal(["COM9"], probed);
    }

    private sealed class ProbeChannel(SerialPortDescriptor port, List<string> probed) : IModbusChannel
    {
        public Task<byte[]> TransceiveAsync(
            byte[] request,
            int responseLength,
            CancellationToken cancellationToken)
        {
            probed.Add(port.PortName);
            return Task.FromResult(ModbusRtuCodec.WithCrc([
                0x01, 0x03, 0x14,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x02, 0x02, 0, 0, 0, 0, 0, 0]));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
