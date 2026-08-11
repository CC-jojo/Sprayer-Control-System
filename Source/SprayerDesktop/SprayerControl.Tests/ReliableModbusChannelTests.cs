using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class ReliableModbusChannelTests
{
    [Fact]
    public async Task RetriesTimeoutAndReturnsTheFirstSuccessfulResponse()
    {
        var inner = new FlakyChannel(2, [1, 3, 0, 0]);
        await using var channel = new ReliableModbusChannel(inner, 3, TimeSpan.Zero);

        byte[] response = await channel.TransceiveAsync([1, 3], 4);

        Assert.Equal([1, 3, 0, 0], response);
        Assert.Equal(3, inner.Attempts);
    }

    [Fact]
    public async Task StopsAfterConfiguredAttemptCount()
    {
        var inner = new FlakyChannel(int.MaxValue, []);
        await using var channel = new ReliableModbusChannel(inner, 3, TimeSpan.Zero);

        await Assert.ThrowsAsync<TimeoutException>(() => channel.TransceiveAsync([1, 3], 4));
        Assert.Equal(3, inner.Attempts);
    }

    private sealed class FlakyChannel(int failuresBeforeSuccess, byte[] response) : IModbusChannel
    {
        public int Attempts { get; private set; }

        public Task<byte[]> TransceiveAsync(byte[] request, int responseLength,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (Attempts <= failuresBeforeSuccess)
                throw new TimeoutException("no response");
            return Task.FromResult(response);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
