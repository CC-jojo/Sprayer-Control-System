using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ControllerClientTests
{
    [Fact]
    public async Task WritesAllSettingsAsOneConsistentBatch()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);
        var settings = ControllerSettings.Default with
        {
            Mode = SprayMode.Groups234,
            Duty = 55,
            TargetCurrentMicroamps = 12500
        };

        await client.WriteSettingsAsync(settings);

        Assert.Equal(0x10, channel.LastRequest[1]);
        Assert.Equal(10, (channel.LastRequest[4] << 8) | channel.LastRequest[5]);
        Assert.Equal(12500, (channel.LastRequest[25] << 8) | channel.LastRequest[26]);
    }

    [Fact]
    public async Task InvalidSettingsAreNotSent()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.WriteSettingsAsync(ControllerSettings.Default with { Duty = 0 }));
        Assert.Empty(channel.LastRequest);
    }

    [Fact]
    public async Task WritesPwmMasterEnableAsSingleRegister()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);

        await client.SetPwmEnabledAsync(true);

        Assert.Equal(0x06, channel.LastRequest[1]);
        Assert.Equal(0x000B, (channel.LastRequest[2] << 8) | channel.LastRequest[3]);
        Assert.Equal(1, (channel.LastRequest[4] << 8) | channel.LastRequest[5]);
    }

    [Fact]
    public async Task ReadsExtendedVersionTwoStatus()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([
                0x01, 0x03, 0x14,
                0x00, 0x2A, 0x00, 0x01, 0x0F, 0xA0, 0x00, 0x00,
                0x00, 0x02, 0x00, 0x00, 0x02, 0x00, 0x00, 0x01, 0x00, 0xFC, 0x00, 0x00])
        };
        var client = new ControllerClient(channel, 1);

        ControllerStatus status = await client.ReadStatusAsync();

        Assert.True(status.PwmEnabled);
        Assert.Equal(0xFC, status.OutputMask);
    }

    [Fact]
    public async Task SaveCommandUsesFirmwareSafetyKey()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);

        await client.SaveToFlashAsync();

        Assert.Equal(0x000A, (channel.LastRequest[2] << 8) | channel.LastRequest[3]);
        Assert.Equal(0xA55A, (channel.LastRequest[4] << 8) | channel.LastRequest[5]);
    }

    private sealed class FakeChannel : IModbusChannel
    {
        public byte[] LastRequest { get; private set; } = [];
        public byte[] Response { get; init; } = [];

        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (Response.Length != 0) return Task.FromResult(Response);
            byte[] response = request[1] == 0x10
                ? ModbusRtuCodec.WithCrc(request.AsSpan(0, 6))
                : request;
            return Task.FromResult(response);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
