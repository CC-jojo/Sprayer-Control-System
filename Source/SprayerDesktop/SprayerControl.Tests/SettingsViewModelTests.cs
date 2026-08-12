using SprayerControl.App.Services;
using SprayerControl.App.ViewModels;
using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task SaveBuildsOneUtcSchemaOneConfigurationFromEditableCopies()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        settings.PwmDutyMinimum = 45;
        settings.ReturnValveSafetyMinimum = 30;
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);

        bool saved = await settings.SaveAsync();

        Assert.True(saved);
        HostConfiguration configuration = Assert.IsType<HostConfiguration>(settings.SavedConfiguration);
        Assert.Equal(1, store.SaveCount);
        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal(45, configuration.PwmDutyMinimum);
        Assert.Equal(30, configuration.ReturnValveSafetyMinimum);
        Assert.Equal(TimeSpan.Zero, configuration.LastModifiedUtc.Offset);
        Assert.Empty(settings.ValidationError);
        Assert.Equal(10, settings.ValidatedCurvePoints.Count);
    }

    [Fact]
    public async Task InvalidDirectionDoesNotWriteAndExposesValidationError()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);
        settings.SetCalibrationCurrent(50, 0.2);

        bool saved = await settings.SaveAsync();

        Assert.False(saved);
        Assert.Equal(0, store.SaveCount);
        Assert.Contains("monotonic", settings.ValidationError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadCopiesOptionalConfigurationForEditing()
    {
        HostConfiguration configuration = JsonHostConfigurationStoreTests.CreateConfiguration(44);
        var store = new RecordingStore { LoadResult = configuration };
        var settings = new SettingsViewModel(store);

        await settings.LoadAsync();
        settings.SetCalibrationCurrent(10, 9.9);

        Assert.Equal(44, settings.PwmDutyMinimum);
        Assert.Equal(0.1, configuration.ReturnValveCalibration.Points[0].CurrentMilliamps);
        Assert.Equal(9.9, settings.CalibrationCurrents[0]);
    }

    private sealed class RecordingStore : IHostConfigurationStore
    {
        public HostConfiguration? LoadResult { get; set; }
        public int SaveCount { get; private set; }

        public Task<HostConfiguration?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(LoadResult);

        public Task SaveAsync(HostConfiguration configuration, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            LoadResult = configuration;
            return Task.CompletedTask;
        }
    }
}
