using SprayerControl.Core;

namespace SprayerControl.App.Services;

public sealed class DemoControllerSession : IControllerSession
{
    private readonly TimeSpan _confirmationDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private Exception? _nextFailure;
    private int _disposed;
    private NozzleMask _commandedMask;
    private bool _pwmEnabled;
    private ushort _duty = 30;
    private ushort _targetCurrentMicroamps;

    public DemoControllerSession(
        ControllerCapabilities? capabilities = null,
        TimeSpan? confirmationDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        Capabilities = capabilities ?? new ControllerCapabilities(2, ControllerCapabilities.ExplicitNozzleMaskFeature);
        _confirmationDelay = confirmationDelay ?? TimeSpan.FromMilliseconds(100);
        _delayAsync = delayAsync ?? Task.Delay;
        IsConnected = true;
    }

    public bool IsConnected { get; private set; }

    public ControllerCapabilities Capabilities { get; }

    public void FailNextOperation(Exception? exception = null)
    {
        ThrowIfDisposed();
        Interlocked.Exchange(ref _nextFailure, exception ?? new InvalidOperationException("Demo controller operation failed."));
    }

    public Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default) =>
        ConfirmAsync(() => _commandedMask = mask, cancellationToken);

    public Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        ConfirmAsync(() => _pwmEnabled = enabled, cancellationToken);

    public Task WriteOperatingSetpointsAsync(
        ushort duty,
        ushort targetCurrentMicroamps,
        CancellationToken cancellationToken = default)
    {
        ValidateOperatingSetpoints(duty, targetCurrentMicroamps);
        return ConfirmAsync(() =>
        {
            _duty = duty;
            _targetCurrentMicroamps = targetCurrentMicroamps;
        }, cancellationToken);
    }

    public Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ushort outputMask = _pwmEnabled ? _commandedMask.Value : (ushort)0;
        return Task.FromResult(new ControllerStatus(
            0, 0, _targetCurrentMicroamps, 0, 0, 0, 0x0202, _pwmEnabled, outputMask, false));
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            IsConnected = false;
        return ValueTask.CompletedTask;
    }

    private async Task ConfirmAsync(Action apply, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        await _mutationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            await _delayAsync(_confirmationDelay, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            Exception? failure = Interlocked.Exchange(ref _nextFailure, null);
            if (failure is not null)
                throw failure;

            apply();
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(DemoControllerSession));
    }

    private static void ValidateOperatingSetpoints(ushort duty, ushort targetCurrentMicroamps)
    {
        if (duty is < 30 or > 80)
            throw new ArgumentOutOfRangeException(nameof(duty), "Duty must be from 30 through 80 percent.");
        if (targetCurrentMicroamps > 20000)
            throw new ArgumentOutOfRangeException(nameof(targetCurrentMicroamps), "Current must be from 0 through 20000 microamps.");
    }
}
