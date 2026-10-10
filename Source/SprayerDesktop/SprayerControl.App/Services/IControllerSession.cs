using SprayerControl.Core;

namespace SprayerControl.App.Services;

public interface IControllerSession : IAsyncDisposable
{
    bool IsConnected { get; }
    ControllerCapabilities Capabilities { get; }
    ControllerParameterLimits ParameterLimits { get; }

    /* 设置界面开关→物理电磁阀的八路映射（bit0=左一…bit7=右四）；仅真实会话生效。 */
    void UpdateNozzleMapping(byte[] uiToPhysical) { }

    Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default);
    Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task WriteOperatingSetpointsAsync(
        ushort duty,
        ushort targetCurrentMicroamps,
        CancellationToken cancellationToken = default);
    Task SavePowerOnDefaultsAsync(
    NozzleMask mask,
    ushort duty,
    ushort targetCurrentMicroamps,
    CancellationToken cancellationToken = default) =>
    Task.FromException(new NotSupportedException("当前会话不支持保存控制器上电默认值。"));
    Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default);
}
