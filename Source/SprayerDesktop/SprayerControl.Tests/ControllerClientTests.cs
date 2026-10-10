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

    [Fact]
    public async Task WritesExplicitNozzleMaskAsOneEchoedRegister()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);

        await client.SetNozzleMaskAsync(new NozzleMask(0xA5));

        Assert.Equal(0x06, channel.LastRequest[1]);
        Assert.Equal(0x000C, (channel.LastRequest[2] << 8) | channel.LastRequest[3]);
        Assert.Equal(0x00A5, (channel.LastRequest[4] << 8) | channel.LastRequest[5]);
    }

    [Fact]
    public async Task CancelledExplicitNozzleMaskIsNotSent()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SetNozzleMaskAsync(new NozzleMask(0xA5), cancellation.Token));

        Assert.Empty(channel.LastRequest);
    }

    [Fact]
    public async Task ReadsProtocolCapabilitiesFromTheAllocatedRegisters()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x02, 0x80, 0x01])
        };
        var client = new ControllerClient(channel, 1);

        ControllerCapabilities capabilities = await client.ReadCapabilitiesAsync();

        Assert.Equal(0x010A, (channel.LastRequest[2] << 8) | channel.LastRequest[3]);
        Assert.Equal(2, (channel.LastRequest[4] << 8) | channel.LastRequest[5]);
        Assert.Equal((ushort)2, capabilities.ProtocolVersion);
        Assert.Equal((ushort)0x8001, capabilities.FeatureFlags);
        Assert.True(capabilities.SupportsExplicitNozzleMask);
    }

    [Fact]
    public async Task CapabilityIllegalAddressFallsBackToLegacyOnly()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x83, 0x02])
        };

        ControllerCapabilities capabilities = await new ControllerClient(channel).ReadCapabilitiesAsync();

        Assert.Equal(ControllerCapabilities.Legacy, capabilities);
    }

    [Fact]
    public async Task PartialIllegalAddressCapabilityFrameDoesNotFallBackToLegacy()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x83])
        };

        ModbusProtocolException exception = await Assert.ThrowsAsync<ModbusProtocolException>(() =>
            new ControllerClient(channel).ReadCapabilitiesAsync());

        Assert.Null(exception.ExceptionCode);
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x03)]
    public async Task CapabilityExceptionsOtherThanIllegalAddressPropagate(byte exceptionCode)
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x83, exceptionCode])
        };

        ModbusProtocolException exception = await Assert.ThrowsAsync<ModbusProtocolException>(() =>
            new ControllerClient(channel).ReadCapabilitiesAsync());

        Assert.Equal(exceptionCode, exception.ExceptionCode);
    }

    [Theory]
    [InlineData(0x02, 0x04, 0x00, 0x02)]
    [InlineData(0x02, 0x03, 0x00, 0x02)]
    public async Task MalformedCapabilityResponsesPropagate(byte slave, byte function, byte high, byte low)
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([slave, function, 0x02, high, low])
        };

        await Assert.ThrowsAsync<ModbusProtocolException>(() =>
            new ControllerClient(channel).ReadCapabilitiesAsync());
    }

    [Fact]
    public async Task CapabilityTimeoutPropagates()
    {
        var client = new ControllerClient(new TimeoutChannel());

        await Assert.ThrowsAsync<TimeoutException>(() => client.ReadCapabilitiesAsync());
    }

    [Fact]
    public async Task WritesOperatingSetpointsAsTwoBatchesSkippingPersistenceFlag()
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);

        await client.WriteOperatingSetpointsAsync(55, 12500);

        // 第一段：0x0001 起写 7 个寄存器（duty 等），不触碰 0x0008。
        Assert.Equal(0x10, channel.Requests[0][1]);
        Assert.Equal(0x0001, (channel.Requests[0][2] << 8) | channel.Requests[0][3]);
        Assert.Equal(7, (channel.Requests[0][4] << 8) | channel.Requests[0][5]);
        Assert.Equal(14, channel.Requests[0][6]);
        Assert.Equal(new byte[]
        {
            0x00, 0x37, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x1E, 0x00, 0x50
        }, channel.Requests[0][7..^2]);

        // 第二段：0x0009 起写 1 个寄存器（目标电流）。
        Assert.Equal(0x10, channel.Requests[1][1]);
        Assert.Equal(0x0009, (channel.Requests[1][2] << 8) | channel.Requests[1][3]);
        Assert.Equal(1, (channel.Requests[1][4] << 8) | channel.Requests[1][5]);
        Assert.Equal(new byte[] { 0x30, 0xD4 }, channel.Requests[1][7..^2]);
    }

    [Theory]
    [InlineData(29, 0)]
    [InlineData(81, 0)]
    [InlineData(30, 20001)]
    public async Task InvalidOperatingSetpointsAreNotSent(ushort duty, ushort current)
    {
        var channel = new FakeChannel();
        var client = new ControllerClient(channel, 1);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.WriteOperatingSetpointsAsync(duty, current));

        Assert.Empty(channel.LastRequest);
    }

    [Fact]
    public async Task ReadsParameterLimitsFromCapabilityRegisters()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x06, 0x00, 0x1E, 0x00, 0x50, 0x4E, 0x20])
        };
        var client = new ControllerClient(channel, 1);

        ControllerParameterLimits limits = await client.ReadParameterLimitsAsync();

        Assert.Equal(0x010C, (channel.LastRequest[2] << 8) | channel.LastRequest[3]);
        Assert.Equal(3, (channel.LastRequest[4] << 8) | channel.LastRequest[5]);
        Assert.Equal(new ControllerParameterLimits(30, 80, 20000), limits);
        Assert.Equal(new ControllerParameterLimits(30, 80, 20000), client.ParameterLimits);
    }

    [Fact]
    public async Task ParameterLimitsIllegalAddressFallsBackToDefaults()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x83, 0x02])
        };

        ControllerParameterLimits limits = await new ControllerClient(channel).ReadParameterLimitsAsync();

        Assert.Equal(ControllerParameterLimits.Default, limits);
    }

    [Fact]
    public async Task ParameterLimitsReflectedInOperatingSetpointsBatch()
    {
        var channel = new FakeChannel
        {
            Response = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x06, 0x00, 0x28, 0x00, 0x64, 0x27, 0x10])
        };
        var client = new ControllerClient(channel, 1);
        await client.ReadParameterLimitsAsync();

        await client.WriteOperatingSetpointsAsync(50, 5000);

        // 第一段 0x0001 起 7 个寄存器：duty=50、min=40、max=100。
        Assert.Equal(new byte[]
        {
            0x00, 0x32, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x28, 0x00, 0x64
        }, channel.Requests[^2][7..^2]);
    }

    private sealed class FakeChannel : IModbusChannel
    {
        public List<byte[]> Requests { get; } = [];
        public byte[] LastRequest { get; private set; } = [];
        public byte[] Response { get; init; } = [];

        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            LastRequest = request;
            if (request[1] == 0x10)
                return Task.FromResult(ModbusRtuCodec.WithCrc(request.AsSpan(0, 6)));
            if (request[1] == 0x06)
                return Task.FromResult((byte[])request.Clone());
            if (Response.Length != 0) return Task.FromResult(Response);
            return Task.FromResult((byte[])request.Clone());
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TimeoutChannel : IModbusChannel
    {
        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength, CancellationToken cancellationToken) =>
            Task.FromException<byte[]>(new TimeoutException("timed out"));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
