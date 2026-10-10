using SprayerControl.App.Services;
using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ControllerSessionTests
{
    [Fact]
    public async Task ModbusSessionReadsAndCachesCapabilitiesExactlyOnce()
    {
        var channel = new SessionChannel
        {
            CapabilityResponse = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x02, 0x00, 0x01])
        };

        await using ModbusControllerSession session = await ModbusControllerSession.CreateAsync(new ControllerClient(channel));

        Assert.True(session.IsConnected);
        Assert.True(session.Capabilities.SupportsExplicitNozzleMask);
        Assert.Equal(1, channel.CapabilityReads);
        Assert.Equal((ushort)2, session.Capabilities.ProtocolVersion);
        Assert.Equal(new ControllerParameterLimits(30, 80, 20000), session.ParameterLimits);
    }

    [Fact]
    public async Task LegacyModbusSessionRejectsExplicitMaskWithFirmwareSupportMessage()
    {
        var channel = new SessionChannel
        {
            CapabilityResponse = ModbusRtuCodec.WithCrc([0x01, 0x83, 0x02])
        };
        await using ModbusControllerSession session = await ModbusControllerSession.CreateAsync(new ControllerClient(channel));

        NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            session.SetNozzleMaskAsync(new NozzleMask(0xA5)));

        Assert.Contains("固件", exception.Message);
        Assert.Equal(1, channel.CapabilityReads);
        Assert.Empty(channel.MaskWrites);
    }

    [Fact]
    public async Task ModbusSessionDelegatesSupportedOperationsAndDisposesOwnedLifetimeOnce()
    {
        var channel = new SessionChannel
        {
            CapabilityResponse = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x02, 0x00, 0x01])
        };
        var owned = new CountingDisposable();
        ModbusControllerSession session = await ModbusControllerSession.CreateAsync(new ControllerClient(channel), owned);

        Assert.Equal(0, owned.DisposeCount);
        await session.SetNozzleMaskAsync(new NozzleMask(0xA5));
        await session.SetPwmEnabledAsync(true);
        await session.WriteOperatingSetpointsAsync(55, 12500);
        await session.DisposeAsync();
        await session.DisposeAsync();

        // 界面 0xA5 按接线映射为物理 0x63 后写固件。
        Assert.Equal(new byte[] { 0x63 }, channel.MaskWrites);
        Assert.Equal(1, owned.DisposeCount);
        Assert.False(session.IsConnected);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.ReadStatusAsync());
    }

    [Fact]
    public async Task PowerOnDefaultUsesCustomPersistentSettingsThenMaskThenSafetyKey()
    {
        var channel = new SessionChannel { CapabilityResponse = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x02, 0x00, 0x01]) };
        await using ModbusControllerSession session = await ModbusControllerSession.CreateAsync(new ControllerClient(channel));

        await session.SavePowerOnDefaultsAsync(new NozzleMask(0xF0), 60, 7800);

        Assert.Equal(["10:0000", "06:000C", "06:000A"], channel.Writes);
    }

    [Fact]
    public async Task ModbusSessionMapsNozzleMaskBetweenUiAndPhysicalAtTheFirmwareBoundary()
    {
        var channel = new SessionChannel
        {
            CapabilityResponse = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x02, 0x00, 0x01]),
            StatusOutputMask = 0x20
        };
        await using ModbusControllerSession session = await ModbusControllerSession.CreateAsync(new ControllerClient(channel));

        // 界面 左一(bit0) 下发 → 固件物理 左四(bit6)。
        await session.SetNozzleMaskAsync(new NozzleMask(0x01));
        Assert.Equal((byte)0x40, Assert.Single(channel.MaskWrites));

        // 固件物理 bit5 读回 → 界面 bit7（右侧第4组）。
        ControllerStatus status = await session.ReadStatusAsync();
        Assert.Equal((ushort)0x80, status.OutputMask);
    }

    [Fact]
    public async Task UpdateNozzleMappingReplacesTheFieldMappingForBothDirections()
    {
        var channel = new SessionChannel
        {
            CapabilityResponse = ModbusRtuCodec.WithCrc([0x01, 0x03, 0x04, 0x00, 0x02, 0x00, 0x01]),
            StatusOutputMask = 0x01,
        };
        await using ModbusControllerSession session = await ModbusControllerSession.CreateAsync(new ControllerClient(channel));

        // 恒等映射：界面 左一(bit0) → 物理 bit0。
        session.UpdateNozzleMapping([0, 1, 2, 3, 4, 5, 6, 7]);
        await session.SetNozzleMaskAsync(new NozzleMask(0x01));
        Assert.Equal((byte)0x01, Assert.Single(channel.MaskWrites));
        ControllerStatus identityStatus = await session.ReadStatusAsync();
        Assert.Equal((ushort)0x01, identityStatus.OutputMask);

        // 换映射：界面 左一(bit0) → 物理 左三(bit4)；物理 bit0 → 界面 左三(bit4)。
        session.UpdateNozzleMapping([4, 1, 2, 3, 0, 5, 6, 7]);
        await session.SetNozzleMaskAsync(new NozzleMask(0x01));
        Assert.Equal((byte)0x10, channel.MaskWrites[^1]);
        ControllerStatus remapped = await session.ReadStatusAsync();
        Assert.Equal((ushort)0x10, remapped.OutputMask);
    }

    [Fact]
    public async Task DemoSessionHardBlocksPowerOnDefaultPersistence()
    {
        await using var session = new DemoControllerSession(delayAsync: static (_, _) => Task.CompletedTask);
        NotSupportedException error = await Assert.ThrowsAsync<NotSupportedException>(() =>
            session.SavePowerOnDefaultsAsync(new NozzleMask(0xF0), 60, 7800));
        Assert.Contains("演示模式", error.Message);
    }

    [Fact]
    public async Task ModbusSessionCreationDisposesOwnedLifetimeAndPropagatesProtocolFailure()
    {
        var owned = new CountingDisposable();
        var channel = new FailureChannel(new ModbusProtocolException("invalid capability response"));

        await Assert.ThrowsAsync<ModbusProtocolException>(() =>
            ModbusControllerSession.CreateAsync(new ControllerClient(channel), owned));

        Assert.Equal(1, owned.DisposeCount);
    }

    [Fact]
    public async Task ModbusSessionCreationDisposesOwnedLifetimeAndPropagatesTimeout()
    {
        var owned = new CountingDisposable();
        var channel = new FailureChannel(new TimeoutException("capability timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ModbusControllerSession.CreateAsync(new ControllerClient(channel), owned));

        Assert.Equal(1, owned.DisposeCount);
    }

    [Fact]
    public async Task ModbusSessionCreationDisposesOwnedLifetimeAndPropagatesCancellation()
    {
        var owned = new CountingDisposable();
        var channel = new SessionChannel();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ModbusControllerSession.CreateAsync(new ControllerClient(channel), owned, cancellation.Token));

        Assert.Equal(1, owned.DisposeCount);
    }

    [Fact]
    public async Task DemoSessionGatesActualOutputMaskByPwmAndDefaultsToProtocolV2()
    {
        await using var session = new DemoControllerSession(delayAsync: static (_, _) => Task.CompletedTask);

        Assert.True(session.Capabilities.SupportsExplicitNozzleMask);
        await session.SetNozzleMaskAsync(new NozzleMask(0xA5));
        Assert.Equal((ushort)0, (await session.ReadStatusAsync()).OutputMask);

        await session.SetPwmEnabledAsync(true);
        Assert.Equal((ushort)0xA5, (await session.ReadStatusAsync()).OutputMask);

        await session.SetPwmEnabledAsync(false);
        Assert.Equal((ushort)0, (await session.ReadStatusAsync()).OutputMask);
    }

    [Fact]
    public async Task DemoSessionCancellationAndInjectedFailureDoNotMutateConfirmedState()
    {
        var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = new DemoControllerSession(delayAsync: async (_, token) =>
        {
            delayStarted.TrySetResult();
            await allowDelay.Task.WaitAsync(token);
        });

        using var cancellation = new CancellationTokenSource();
        Task cancelledWrite = session.SetNozzleMaskAsync(new NozzleMask(0xA5), cancellation.Token);
        await delayStarted.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWrite);
        Assert.Equal((ushort)0, (await session.ReadStatusAsync()).OutputMask);

        session.FailNextOperation(new InvalidOperationException("simulated"));
        allowDelay.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SetPwmEnabledAsync(true));
        Assert.False((await session.ReadStatusAsync()).PwmEnabled);

        await session.SetPwmEnabledAsync(true);
        Assert.True((await session.ReadStatusAsync()).PwmEnabled);
    }

    [Fact]
    public async Task DemoSessionValidatesSetpointsAndRejectsUseAfterDisposal()
    {
        var session = new DemoControllerSession(delayAsync: static (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.WriteOperatingSetpointsAsync(29, 0));
        await session.WriteOperatingSetpointsAsync(55, 12500);
        Assert.Equal((ushort)12500, (await session.ReadStatusAsync()).TargetCurrentMicroamps);

        await session.DisposeAsync();
        await session.DisposeAsync();
        Assert.False(session.IsConnected);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.SetPwmEnabledAsync(true));
    }

    [Fact]
    public async Task DemoSessionSerializesMutationsAndKeepsFailureForTheAdmittedOperation()
    {
        var firstDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondDelayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int delayCall = 0;
        await using var session = new DemoControllerSession(delayAsync: async (_, token) =>
        {
            int currentCall = Interlocked.Increment(ref delayCall);
            if (currentCall == 1)
            {
                firstDelayStarted.TrySetResult();
                await releaseFirstDelay.Task.WaitAsync(token);
            }
            else if (currentCall == 2)
            {
                secondDelayStarted.TrySetResult();
                await releaseSecondDelay.Task.WaitAsync(token);
            }
        });
        session.FailNextOperation(new InvalidOperationException("first operation fails"));

        Task first = session.SetNozzleMaskAsync(new NozzleMask(0xA5));
        await firstDelayStarted.Task;
        using var waitingCancellation = new CancellationTokenSource();
        Task waiting = session.SetPwmEnabledAsync(true, waitingCancellation.Token);

        Assert.False(secondDelayStarted.Task.IsCompleted);
        waitingCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        releaseFirstDelay.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);

        Task succeeding = session.SetPwmEnabledAsync(true);
        await secondDelayStarted.Task;
        releaseSecondDelay.SetResult();
        await succeeding;
        Assert.True((await session.ReadStatusAsync()).PwmEnabled);
        Assert.Equal((ushort)0, (await session.ReadStatusAsync()).OutputMask);
    }

    [Fact]
    public async Task DemoSessionDisposalDuringPendingMutationPreventsConfirmation()
    {
        var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseDelay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = new DemoControllerSession(delayAsync: async (_, token) =>
        {
            delayStarted.TrySetResult();
            await releaseDelay.Task.WaitAsync(token);
        });

        Task pending = session.SetNozzleMaskAsync(new NozzleMask(0xA5));
        await delayStarted.Task;
        await session.DisposeAsync();
        releaseDelay.SetResult();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.False(session.IsConnected);
    }

    private sealed class SessionChannel : IModbusChannel
    {
        public byte[] CapabilityResponse { get; init; } = [];
        public byte StatusOutputMask { get; init; }
        public int CapabilityReads { get; private set; }
        public List<byte> MaskWrites { get; } = [];
        public List<string> Writes { get; } = [];

        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request[1] == 0x03 && request[2] == 0x01 && request[3] == 0x0A)
            {
                CapabilityReads++;
                return Task.FromResult(CapabilityResponse);
            }

            /* 参数限制能力区（0x010C~0x010E）：返回 30 / 80 / 20000。 */
            if (request[1] == 0x03 && request[2] == 0x01 && request[3] == 0x0C)
            {
                return Task.FromResult(ModbusRtuCodec.WithCrc([
                    0x01, 0x03, 0x06, 0x00, 0x1E, 0x00, 0x50, 0x4E, 0x20]));
            }

            if (request[1] == 0x06 && request[2] == 0x00 && request[3] == 0x0C)
                MaskWrites.Add(request[5]);
            if (request[1] is 0x06 or 0x10)
                Writes.Add($"{request[1]:X2}:{request[2]:X2}{request[3]:X2}");

            if (request[1] == 0x03)
            {
                return Task.FromResult(ModbusRtuCodec.WithCrc([
                    0x01, 0x03, 0x14,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x02, 0x02, 0, 1,
                    (byte)(StatusOutputMask >> 8), (byte)StatusOutputMask, 0, 0]));
            }

            return Task.FromResult(request[1] == 0x10
                ? ModbusRtuCodec.WithCrc(request.AsSpan(0, 6))
                : request);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingDisposable : IAsyncDisposable
    {
        public int DisposeCount { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FailureChannel(Exception exception) : IModbusChannel
    {
        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength, CancellationToken cancellationToken = default) =>
            Task.FromException<byte[]>(exception);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
