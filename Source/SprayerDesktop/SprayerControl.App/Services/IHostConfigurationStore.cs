using SprayerControl.Core;

namespace SprayerControl.App.Services;

/// <summary>Loads and saves the host-only configuration without any credentials.</summary>
public interface IHostConfigurationStore
{
    Task<HostConfiguration?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(HostConfiguration configuration, CancellationToken cancellationToken = default);
}
