using SprayerControl.Core;

namespace SprayerControl.App.Services;

public interface IControllerSession : IAsyncDisposable
{
    bool IsConnected { get; }
    ControllerCapabilities Capabilities { get; }

    Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default);
    Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task WriteOperatingSetpointsAsync(
        ushort duty,
        ushort targetCurrentMicroamps,
        CancellationToken cancellationToken = default);
    Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default);
}
