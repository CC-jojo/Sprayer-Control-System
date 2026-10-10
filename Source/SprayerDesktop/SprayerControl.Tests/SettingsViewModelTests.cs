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
        Assert.Equal(NozzleMask.DefaultUiToPhysicalBit, configuration.NozzleMapping);
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
        Assert.Contains("单调", settings.ValidationError);
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

    [Fact]
    public async Task DemoModeHardBlocksLocalPersistence()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store, JsonHostConfigurationStoreTests.CreateConfiguration(), allowPersistence: false);

        bool saved = await settings.SaveAsync();

        Assert.False(saved);
        Assert.Equal(0, store.SaveCount);
        Assert.False(settings.CanSaveLocal);
        Assert.Contains("演示模式", settings.ValidationError);
    }

    [Fact]
    public async Task MappingSelectionsRoundTripIntoSavedConfiguration()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        settings.SetNozzleMappingSelection(0, 5);
        settings.SetNozzleMappingSelection(1, 4);
        settings.SetNozzleMappingSelection(2, 7);
        settings.SetNozzleMappingSelection(3, 2);
        settings.SetNozzleMappingSelection(4, 3);
        settings.SetNozzleMappingSelection(5, 6);
        settings.SetNozzleMappingSelection(6, 1);
        settings.SetNozzleMappingSelection(7, 0);
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);

        bool saved = await settings.SaveAsync();

        Assert.True(saved);
        Assert.Equal(new byte[] { 5, 4, 7, 2, 3, 6, 1, 0 }, settings.SavedConfiguration!.NozzleMapping);
    }

    [Fact]
    public async Task DuplicateMappingSelectionsAreRejectedWithoutSaving()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        settings.SetNozzleMappingSelection(0, 3);
        settings.SetNozzleMappingSelection(1, 3);
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);

        bool saved = await settings.SaveAsync();

        Assert.False(saved);
        Assert.Equal(0, store.SaveCount);
        Assert.Contains("重复", settings.ValidationError);
    }

    [Fact]
    public void ResetNozzleMappingRestoresTheDefaultTable()
    {
        var settings = new SettingsViewModel(new RecordingStore());
        settings.SetNozzleMappingSelection(0, 7);
        settings.ResetNozzleMapping();

        Assert.Equal(
            NozzleMask.DefaultUiToPhysicalBit,
            settings.NozzleMappingEntries.Select(entry => (byte)entry.SelectedPhysicalIndex).ToArray());
    }

    [Fact]
    public void CalibrationCompletenessTracksFilledPoints()
    {
        var settings = new SettingsViewModel(new RecordingStore());
        Assert.False(settings.IsCalibrationComplete);

        for (int opening = 10; opening <= 90; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);
        Assert.False(settings.IsCalibrationComplete);

        settings.SetCalibrationCurrent(100, 1.0);
        Assert.True(settings.IsCalibrationComplete);
    }

    [Fact]
    public async Task LastDutyPercentIsPersistedAndReloaded()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        settings.LastDutyPercent = 72;
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);

        Assert.True(await settings.SaveAsync());
        Assert.Equal(72, settings.SavedConfiguration!.LastDutyPercent);

        var reloaded = new SettingsViewModel(store, settings.SavedConfiguration);
        Assert.Equal(72, reloaded.LastDutyPercent);
    }

    [Fact]
    public void MappingEntriesLoadFromTheSavedConfiguration()
    {
        byte[] custom = [0, 1, 2, 3, 4, 5, 6, 7];
        HostConfiguration configuration = new(
            1, 30, 10,
            new ReturnValveCalibration(JsonHostConfigurationStoreTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            custom);
        var settings = new SettingsViewModel(new RecordingStore(), configuration);

        Assert.Equal(
            custom,
            settings.NozzleMappingEntries.Select(entry => (byte)entry.SelectedPhysicalIndex).ToArray());
        Assert.Equal("左一", settings.NozzleMappingEntries[0].UiLabel);
        Assert.Equal(8, settings.NozzleMappingEntries.Count);
    }

    [Fact]
    public async Task PowerOnReturnValveSettingsArePersistedAndReloaded()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        settings.ReturnValveSafetyMinimum = 30;
        settings.PowerOnReturnValveEnabled = true;
        settings.PowerOnReturnValvePercent = 65;
        settings.LastReturnOpeningPercent = 45;
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);

        Assert.True(await settings.SaveAsync());
        HostConfiguration saved = settings.SavedConfiguration!;
        Assert.True(saved.PowerOnReturnValveEnabled);
        Assert.Equal(65, saved.PowerOnReturnValvePercent);
        Assert.Equal(45d, saved.LastReturnOpeningPercent!.Value);

        var reloaded = new SettingsViewModel(store, saved);
        Assert.True(reloaded.PowerOnReturnValveEnabled);
        Assert.Equal(65, reloaded.PowerOnReturnValvePercent);
        Assert.Equal(45d, reloaded.LastReturnOpeningPercent!.Value);
    }

    [Fact]
    public async Task PowerOnReturnValveBelowSafetyFloorIsRaisedOnSave()
    {
        var store = new RecordingStore();
        var settings = new SettingsViewModel(store);
        settings.ReturnValveSafetyMinimum = 50;
        settings.PowerOnReturnValvePercent = 20;
        for (int opening = 10; opening <= 100; opening += 10)
            settings.SetCalibrationCurrent(opening, opening / 100.0);

        Assert.True(await settings.SaveAsync());
        Assert.Equal(50, settings.SavedConfiguration!.PowerOnReturnValvePercent);
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
