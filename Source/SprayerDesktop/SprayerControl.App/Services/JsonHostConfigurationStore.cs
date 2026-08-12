using System.IO;
using System.Text.Json;
using SprayerControl.Core;

namespace SprayerControl.App.Services;

public sealed class JsonHostConfigurationStore : IHostConfigurationStore
{
    private const int AcceptedSchemaVersion = 1;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly Func<string, string, CancellationToken, Task> _replaceAsync;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public JsonHostConfigurationStore(
        string? path = null,
        Func<string, string, CancellationToken, Task>? replaceAsync = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SprayerTouchHMI",
            "host-configuration.json");
        _replaceAsync = replaceAsync ?? ReplaceAtomicallyAsync;
    }

    public async Task<HostConfiguration?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_path))
            return null;

        try
        {
            await using FileStream stream = new(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            HostConfiguration? parsed = await JsonSerializer.DeserializeAsync<HostConfiguration>(
                stream, SerializerOptions, cancellationToken);
            if (parsed is null)
                throw new InvalidDataException(
                    "Host configuration JSON must contain an object.",
                    new JsonException("The JSON root was null."));

            return ValidateAndClone(parsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidDataException exception) when (exception.InnerException is not null)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or IOException)
        {
            throw new InvalidDataException("Host configuration file is invalid.", exception);
        }
    }

    public async Task SaveAsync(HostConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        HostConfiguration validated = ValidateAndClone(configuration);
        await _saveGate.WaitAsync(cancellationToken);
        string? temporaryPath = null;
        try
        {
            string directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Configuration path must include a directory.");
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $"{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
            await WriteTemporaryFileAsync(temporaryPath, validated, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _replaceAsync(temporaryPath, _path, cancellationToken);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                    // The original write/replace failure remains the actionable error.
                }
            }

            _saveGate.Release();
        }
    }

    private static HostConfiguration ValidateAndClone(HostConfiguration configuration)
    {
        if (configuration.SchemaVersion != AcceptedSchemaVersion)
        {
            throw new InvalidDataException(
                $"Host configuration schema must be exactly {AcceptedSchemaVersion}.",
                new ArgumentOutOfRangeException(
                    nameof(configuration.SchemaVersion),
                    configuration.SchemaVersion,
                    "Unsupported configuration schema."));
        }

        try
        {
            return new HostConfiguration(
                configuration.SchemaVersion,
                configuration.PwmDutyMinimum,
                configuration.ReturnValveSafetyMinimum,
                new ReturnValveCalibration(configuration.ReturnValveCalibration.Points),
                configuration.LastModifiedUtc);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException("Host configuration values are invalid.", exception);
        }
    }

    private static async Task WriteTemporaryFileAsync(
        string temporaryPath,
        HostConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        await JsonSerializer.SerializeAsync(stream, configuration, SerializerOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static Task ReplaceAtomicallyAsync(string temporaryPath, string targetPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(targetPath))
            File.Replace(temporaryPath, targetPath, destinationBackupFileName: null);
        else
            File.Move(temporaryPath, targetPath);
        return Task.CompletedTask;
    }
}
