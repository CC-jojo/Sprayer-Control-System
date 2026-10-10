namespace SprayerControl.Core;

public sealed class ReliableModbusChannel(
    IModbusChannel inner,
    int maxAttempts = 3,
    TimeSpan? retryDelay = null) : IModbusChannel
{
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(40);

    public async Task<byte[]> TransceiveAsync(
        byte[] request,
        int responseLength,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await inner.TransceiveAsync(request, responseLength, cancellationToken);
            }
            catch (TimeoutException) when (attempt < maxAttempts)
            {
                if (_retryDelay > TimeSpan.Zero)
                    await Task.Delay(_retryDelay, cancellationToken);
            }
        }
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
