namespace SprayerControl.Core;

public enum ControllerProbeOutcome
{
    Connected,
    OpenOrHandshakeFailed,
    UnsupportedFirmware
}

public sealed record ControllerProbeResult(
    SerialPortDescriptor Port,
    ControllerProbeOutcome Outcome,
    string Detail);

public sealed record ControllerConnection(
    SerialPortDescriptor Port,
    IModbusChannel Channel,
    ControllerClient Client,
    ControllerStatus Status) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Channel.DisposeAsync();
}

public static class ControllerDiscovery
{
    public static async Task<ControllerConnection?> TryDiscoverAsync(
        IEnumerable<SerialPortDescriptor> ports,
        Func<SerialPortDescriptor, IModbusChannel> channelFactory,
        CancellationToken cancellationToken = default,
        Action<ControllerProbeResult>? diagnostic = null,
        string? preferredPortName = null)
    {
        /* 记忆端口优先：上次成功连接的 COM 口排在最前，匹配不到再按原优先级扫描。 */
        IEnumerable<SerialPortDescriptor> ordered = preferredPortName is null
            ? ports.OrderBy(SerialPortSelection.GetRank)
            : ports.OrderBy(port => string.Equals(
                    port.PortName, preferredPortName, StringComparison.OrdinalIgnoreCase)
                ? -1
                : SerialPortSelection.GetRank(port));

        foreach (SerialPortDescriptor port in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IModbusChannel? channel = null;
            try
            {
                channel = channelFactory(port);
                var client = new ControllerClient(channel);
                ControllerStatus status = await client.ReadStatusAsync(cancellationToken);
                if (status.FirmwareVersion is >= 0x0201 and < 0x0300)
                {
                    diagnostic?.Invoke(new ControllerProbeResult(
                        port, ControllerProbeOutcome.Connected,
                        $"Firmware 0x{status.FirmwareVersion:X4}"));
                    return new ControllerConnection(port, channel, client, status);
                }

                diagnostic?.Invoke(new ControllerProbeResult(
                    port, ControllerProbeOutcome.UnsupportedFirmware,
                    $"Firmware 0x{status.FirmwareVersion:X4}"));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                diagnostic?.Invoke(new ControllerProbeResult(
                    port, ControllerProbeOutcome.OpenOrHandshakeFailed,
                    exception.Message));
            }

            if (channel is not null)
                await channel.DisposeAsync();
        }

        return null;
    }
}
