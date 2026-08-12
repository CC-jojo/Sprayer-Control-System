using SprayerControl.Core;

namespace SprayerControl.App.Services;

public sealed class ModbusControllerSession : IControllerSession
{
    private readonly ControllerClient _client;
    private readonly IAsyncDisposable? _ownedLifetime;
    private int _disposed;

    private ModbusControllerSession(
        ControllerClient client,
        ControllerCapabilities capabilities,
        IAsyncDisposable? ownedLifetime)
    {
        _client = client;
        Capabilities = capabilities;
        _ownedLifetime = ownedLifetime;
        IsConnected = true;
    }

    public bool IsConnected { get; private set; }

    public ControllerCapabilities Capabilities { get; }

    public static async Task<ModbusControllerSession> CreateAsync(
        ControllerClient client,
        IAsyncDisposable? ownedLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        try
        {
            ControllerCapabilities capabilities = await client.ReadCapabilitiesAsync(cancellationToken);
            return new ModbusControllerSession(client, capabilities, ownedLifetime);
        }
        catch
        {
            if (ownedLifetime is not null)
            {
                try
                {
                    await ownedLifetime.DisposeAsync();
                }
                catch
                {
                    // Preserve the capability-read failure that triggered cleanup.
                }
            }

            throw;
        }
    }

    public Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Capabilities.SupportsExplicitNozzleMask)
        {
            throw new NotSupportedException(
                "This controller firmware/protocol does not support explicit nozzle masks.");
        }

        return _client.SetNozzleMaskAsync(mask, cancellationToken);
    }

    public Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _client.SetPwmEnabledAsync(enabled, cancellationToken);
    }

    public Task WriteOperatingSetpointsAsync(
        ushort duty,
        ushort targetCurrentMicroamps,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _client.WriteOperatingSetpointsAsync(duty, targetCurrentMicroamps, cancellationToken);
    }

    public async Task SavePowerOnDefaultsAsync(
        NozzleMask mask,
        ushort duty,
        ushort targetCurrentMicroamps,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Capabilities.SupportsExplicitNozzleMask)
            throw new NotSupportedException("The connected firmware cannot persist an explicit eight-nozzle mask.");

        await _client.WriteSettingsAsync(
            new ControllerSettings(SprayMode.Custom, duty, PersistenceEnabled: true, targetCurrentMicroamps),
            cancellationToken);
        await _client.SetNozzleMaskAsync(mask, cancellationToken);
        await _client.SaveToFlashAsync(cancellationToken);
    }

    public Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _client.ReadStatusAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        IsConnected = false;
        if (_ownedLifetime is not null)
            await _ownedLifetime.DisposeAsync();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ModbusControllerSession));
    }
}
