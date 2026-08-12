using SprayerControl.App.Services;
using SprayerControl.Core;
using System.Text.Json;

namespace SprayerControl.Tests;

public sealed class JsonHostConfigurationStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SprayerTouchHMI.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAndLoadRoundTripsValidatedConfigurationWithUtcSortedPoints()
    {
        string path = Path.Combine(_directory, "host-configuration.json");
        DateTimeOffset timestamp = new(2026, 8, 12, 9, 30, 0, TimeSpan.FromHours(8));
        HostConfiguration configuration = CreateConfiguration(45, timestamp);
        var store = new JsonHostConfigurationStore(path);

        await store.SaveAsync(configuration);
        HostConfiguration loaded = Assert.IsType<HostConfiguration>(await store.LoadAsync());

        Assert.Equal(1, loaded.SchemaVersion);
        Assert.Equal(45, loaded.PwmDutyMinimum);
        Assert.Equal(timestamp.ToUniversalTime(), loaded.LastModifiedUtc);
        Assert.Equal(Enumerable.Range(1, 10).Select(index => index * 10), loaded.ReturnValveCalibration.Points.Select(point => point.OpeningPercent));
    }

    [Fact]
    public async Task LoadReturnsNullForAMissingFile()
    {
        var store = new JsonHostConfigurationStore(Path.Combine(_directory, "missing.json"));

        Assert.Null(await store.LoadAsync());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task LoadWrapsMalformedOrNullDocumentsAsInvalidData(string document)
    {
        string path = Path.Combine(_directory, "invalid.json");
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(path, document);

        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonHostConfigurationStore(path).LoadAsync());

        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task LoadRejectsWrongSchemaAndDirectionReversal()
    {
        string path = Path.Combine(_directory, "invalid-values.json");
        Directory.CreateDirectory(_directory);
        HostConfiguration wrongSchema = new(2, 30, 10, new ReturnValveCalibration(ValidPoints()), DateTimeOffset.UtcNow);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(wrongSchema));

        InvalidDataException schemaException = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonHostConfigurationStore(path).LoadAsync());
        Assert.NotNull(schemaException.InnerException);

        CalibrationPoint[] reversed = ValidPoints();
        reversed[4] = new CalibrationPoint(50, 0.2);
        string reversal = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            PwmDutyMinimum = 30,
            ReturnValveSafetyMinimum = 10,
            ReturnValveCalibration = new { Points = reversed },
            LastModifiedUtc = DateTimeOffset.UtcNow,
        });
        await File.WriteAllTextAsync(path, reversal);

        await Assert.ThrowsAsync<InvalidDataException>(() => new JsonHostConfigurationStore(path).LoadAsync());

        string missingPoint = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            PwmDutyMinimum = 30,
            ReturnValveSafetyMinimum = 10,
            ReturnValveCalibration = new { Points = ValidPoints().Take(9) },
            LastModifiedUtc = DateTimeOffset.UtcNow,
        });
        await File.WriteAllTextAsync(path, missingPoint);

        await Assert.ThrowsAsync<InvalidDataException>(() => new JsonHostConfigurationStore(path).LoadAsync());
    }

    [Fact]
    public async Task FailedReplacementPreservesPreviousDocumentAndCleansTemporaryFile()
    {
        string path = Path.Combine(_directory, "host-configuration.json");
        Directory.CreateDirectory(_directory);
        byte[] original = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(path, original);
        var store = new JsonHostConfigurationStore(path, static (_, _, _) => throw new IOException("replacement failed"));

        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(CreateConfiguration()));

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFiles(_directory, "host-configuration.json.*.tmp"));
    }

    [Fact]
    public async Task CancellationBeforeReplacementPreservesPreviousDocumentAndCleansTemporaryFile()
    {
        string path = Path.Combine(_directory, "host-configuration.json");
        Directory.CreateDirectory(_directory);
        byte[] original = [9, 8, 7];
        await File.WriteAllBytesAsync(path, original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new JsonHostConfigurationStore(path).SaveAsync(CreateConfiguration(), cancellation.Token));

        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFiles(_directory, "host-configuration.json.*.tmp"));
    }

    [Fact]
    public async Task ConcurrentSavesLeaveOneValidConfiguration()
    {
        string path = Path.Combine(_directory, "host-configuration.json");
        var store = new JsonHostConfigurationStore(path);

        await Task.WhenAll(
            store.SaveAsync(CreateConfiguration(31)),
            store.SaveAsync(CreateConfiguration(32)));

        HostConfiguration loaded = Assert.IsType<HostConfiguration>(await store.LoadAsync());
        Assert.Contains(loaded.PwmDutyMinimum, [31, 32]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    internal static HostConfiguration CreateConfiguration(int pwmDutyMinimum = 30, DateTimeOffset? timestamp = null) =>
        new(1, pwmDutyMinimum, 20, new ReturnValveCalibration(ValidPoints()), timestamp ?? DateTimeOffset.UtcNow);

    internal static CalibrationPoint[] ValidPoints() => Enumerable.Range(1, 10)
        .Select(index => new CalibrationPoint(index * 10, index / 10.0))
        .ToArray();
}
