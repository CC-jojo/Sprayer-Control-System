using SprayerControl.Core;

namespace SprayerControl.App.Services;

public sealed class ModbusControllerSession : IControllerSession
{
    private readonly ControllerClient _client;
    private readonly IAsyncDisposable? _ownedLifetime;
    private byte[] _uiToPhysical;
    private byte[] _physicalToUi;
    private int _disposed;

    private ModbusControllerSession(
        ControllerClient client,
        ControllerCapabilities capabilities,
        ControllerParameterLimits parameterLimits,
        IAsyncDisposable? ownedLifetime)
    {
        _client = client;
        Capabilities = capabilities;
        ParameterLimits = parameterLimits;
        _ownedLifetime = ownedLifetime;
        IsConnected = true;
        _uiToPhysical = (byte[])NozzleMask.DefaultUiToPhysicalBit.Clone();
        _physicalToUi = NozzleMask.BuildInverseMap(_uiToPhysical);
    }

    /* 应用用户在设置页自定义的八路映射；校验通过后替换当前映射。 */
    public void UpdateNozzleMapping(byte[] uiToPhysical)
    {
        ThrowIfDisposed();
        byte[] validated = NozzleMask.ValidateMapping(uiToPhysical);
        _uiToPhysical = validated;
        _physicalToUi = NozzleMask.BuildInverseMap(validated);
    }

    public bool IsConnected { get; private set; }

    public ControllerCapabilities Capabilities { get; }

    public ControllerParameterLimits ParameterLimits { get; }

    public static async Task<ModbusControllerSession> CreateAsync(
        ControllerClient client,
        IAsyncDisposable? ownedLifetime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        try
        {
            ControllerCapabilities capabilities = await client.ReadCapabilitiesAsync(cancellationToken);
            ControllerParameterLimits limits = await client.ReadParameterLimitsAsync(cancellationToken);
            return new ModbusControllerSession(client, capabilities, limits, ownedLifetime);
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

    /* 与固件收发时在界面位掩码与物理喷头位掩码之间做映射；
       界面显示保持界面位，不参与映射。 */
    public Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Capabilities.SupportsExplicitNozzleMask)
        {
            throw new NotSupportedException("控制器固件/协议不支持独立的八喷头掩码。");
        }

        return _client.SetNozzleMaskAsync(mask.ToPhysical(_uiToPhysical), cancellationToken);
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
            throw new NotSupportedException("当前固件不支持保存独立的八喷头掩码。");

        await _client.WriteSettingsAsync(
            new ControllerSettings(SprayMode.Custom, duty, PersistenceEnabled: true, targetCurrentMicroamps),
            cancellationToken);
        await _client.SetNozzleMaskAsync(mask.ToPhysical(_uiToPhysical), cancellationToken);
        await _client.SaveToFlashAsync(cancellationToken);
    }

    public async Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ControllerStatus status = await _client.ReadStatusAsync(cancellationToken);
        return status with { OutputMask = new NozzleMask((byte)status.OutputMask).ToUi(_physicalToUi).Value };
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
