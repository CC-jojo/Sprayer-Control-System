using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ControllerDiscoveryTests
{
    [Fact]
    public async Task ReturnsFirstPortWithValidControllerHandshakeAndDisposesFailures()
    {
        var factory = new ScriptedFactory(["COM12"], ["COM9"]);

        ControllerConnection? result = await ControllerDiscovery.TryDiscoverAsync(
            [new("COM12", "CH343"), new("COM9", "Generic serial")], factory.Create);

        Assert.NotNull(result);
        Assert.Equal("COM9", result.Port.PortName);
        Assert.Contains("COM12", factory.DisposedPorts);
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ReturnsNullAndDisposesEveryInvalidCandidate()
    {
        var factory = new ScriptedFactory(["COM1", "COM2"], []);

        ControllerConnection? result = await ControllerDiscovery.TryDiscoverAsync(
            [new("COM1", "Other"), new("COM2", "Other")], factory.Create);

        Assert.Null(result);
        Assert.Equal(2, factory.DisposedPorts.Count);
    }

    [Fact]
    public async Task HonorsCancellationBeforeOpeningAPort()
    {
        var factory = new ScriptedFactory([], ["COM9"]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            ControllerDiscovery.TryDiscoverAsync(
                [new("COM9", "CH343")], factory.Create, cancellation.Token));

        Assert.Equal(0, factory.CreatedCount);
    }

    [Fact]
    public async Task ReportsFailureThenConnectionForProbedPorts()
    {
        var factory = new ScriptedFactory(["COM12"], ["COM9"]);
        List<ControllerProbeResult> diagnostics = [];

        ControllerConnection? result = await ControllerDiscovery.TryDiscoverAsync(
            [new("COM12", "Other"), new("COM9", "Other")],
            factory.Create,
            diagnostic: diagnostics.Add);

        Assert.NotNull(result);
        Assert.Equal(
            [ControllerProbeOutcome.OpenOrHandshakeFailed, ControllerProbeOutcome.Connected],
            diagnostics.Select(item => item.Outcome));
        Assert.Equal(["COM12", "COM9"], diagnostics.Select(item => item.Port.PortName));
        await result.DisposeAsync();
    }

    [Fact]
    public async Task ReportsUnsupportedFirmwareVersion()
    {
        var factory = new ScriptedFactory([], []);
        List<ControllerProbeResult> diagnostics = [];

        ControllerConnection? result = await ControllerDiscovery.TryDiscoverAsync(
            [new("COM8", "Other")], factory.Create, diagnostic: diagnostics.Add);

        Assert.Null(result);
        ControllerProbeResult diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ControllerProbeOutcome.UnsupportedFirmware, diagnostic.Outcome);
        Assert.Contains("0x0200", diagnostic.Detail);
    }

    private sealed class ScriptedFactory(
        IEnumerable<string> failingPorts,
        IEnumerable<string> successfulPorts)
    {
        private readonly HashSet<string> _failing = [.. failingPorts];
        private readonly HashSet<string> _successful = [.. successfulPorts];
        public List<string> DisposedPorts { get; } = [];
        public int CreatedCount { get; private set; }

        public IModbusChannel Create(SerialPortDescriptor port)
        {
            CreatedCount++;
            return new ScriptedChannel(port.PortName, _failing.Contains(port.PortName),
                _successful.Contains(port.PortName), DisposedPorts);
        }
    }

    private sealed class ScriptedChannel(
        string port,
        bool timeout,
        bool supportedFirmware,
        List<string> disposedPorts) : IModbusChannel
    {
        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength,
            CancellationToken cancellationToken = default)
        {
            if (timeout) throw new TimeoutException();
            ushort version = supportedFirmware ? (ushort)0x0201 : (ushort)0x0200;
            return Task.FromResult(ModbusRtuCodec.WithCrc([
                0x01, 0x03, 0x14,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, (byte)(version >> 8), (byte)version, 0, 0, 0, 0, 0, 0]));
        }

        public ValueTask DisposeAsync()
        {
            disposedPorts.Add(port);
            return ValueTask.CompletedTask;
        }
    }
}
