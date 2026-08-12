using System.Collections.ObjectModel;
using SprayerControl.App.Services;
using SprayerControl.Core;

namespace SprayerControl.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IHostConfigurationStore _store;
    private readonly ObservableCollection<double?> _calibrationCurrents = Enumerable.Repeat<double?>(null, 10).ToObservableCollection();
    private readonly ReadOnlyObservableCollection<double?> _readOnlyCalibrationCurrents;
    private int _pwmDutyMinimum = 30;
    private int _returnValveSafetyMinimum = 10;
    private string _validationError = string.Empty;
    private HostConfiguration? _savedConfiguration;

    public SettingsViewModel(IHostConfigurationStore store, HostConfiguration? configuration = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _readOnlyCalibrationCurrents = new ReadOnlyObservableCollection<double?>(_calibrationCurrents);
        if (configuration is not null)
            CopyFrom(configuration);
        SaveCommand = new AsyncCommand(async token => await SaveAsync(token), ReportException);
    }

    public AsyncCommand SaveCommand { get; }

    public int PwmDutyMinimum
    {
        get => _pwmDutyMinimum;
        set => SetProperty(ref _pwmDutyMinimum, value);
    }

    public int ReturnValveSafetyMinimum
    {
        get => _returnValveSafetyMinimum;
        set => SetProperty(ref _returnValveSafetyMinimum, value);
    }

    public ReadOnlyObservableCollection<double?> CalibrationCurrents => _readOnlyCalibrationCurrents;

    public IReadOnlyList<CalibrationPoint> ValidatedCurvePoints
    {
        get
        {
            try
            {
                return BuildCalibration().Points;
            }
            catch (Exception exception) when (HasValidationFailure(exception))
            {
                return [];
            }
        }
    }

    public string ValidationError
    {
        get => _validationError;
        private set => SetProperty(ref _validationError, value);
    }

    public HostConfiguration? SavedConfiguration
    {
        get => _savedConfiguration;
        private set => SetProperty(ref _savedConfiguration, value);
    }

    public void SetCalibrationCurrent(int openingPercent, double? currentMilliamps)
    {
        if (openingPercent is < 10 or > 100 || openingPercent % 10 != 0)
            throw new ArgumentOutOfRangeException(nameof(openingPercent), "Calibration openings must be 10 through 100 in increments of 10.");

        _calibrationCurrents[(openingPercent / 10) - 1] = currentMilliamps;
        OnPropertyChanged(nameof(CalibrationCurrents));
        OnPropertyChanged(nameof(ValidatedCurvePoints));
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            HostConfiguration? configuration = await _store.LoadAsync(cancellationToken);
            if (configuration is not null)
            {
                CopyFrom(configuration);
                SavedConfiguration = configuration;
            }

            ValidationError = string.Empty;
        }
        catch (Exception exception)
        {
            ReportException(exception);
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        HostConfiguration configuration;
        try
        {
            configuration = new HostConfiguration(
                schemaVersion: 1,
                pwmDutyMinimum: PwmDutyMinimum,
                returnValveSafetyMinimum: ReturnValveSafetyMinimum,
                returnValveCalibration: BuildCalibration(),
                lastModifiedUtc: DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (HasValidationFailure(exception))
        {
            ValidationError = exception.Message;
            return false;
        }

        try
        {
            await _store.SaveAsync(configuration, cancellationToken);
            SavedConfiguration = configuration;
            ValidationError = string.Empty;
            OnPropertyChanged(nameof(ValidatedCurvePoints));
            return true;
        }
        catch (Exception exception)
        {
            ReportException(exception);
            return false;
        }
    }

    private ReturnValveCalibration BuildCalibration()
    {
        if (_calibrationCurrents.Any(static current => !current.HasValue))
            throw new ArgumentException("All ten calibration currents are required.");

        CalibrationPoint[] points = Enumerable.Range(1, 10)
            .Select(index => new CalibrationPoint(index * 10, _calibrationCurrents[index - 1]!.Value))
            .ToArray();
        return new ReturnValveCalibration(points);
    }

    private void CopyFrom(HostConfiguration configuration)
    {
        PwmDutyMinimum = configuration.PwmDutyMinimum;
        ReturnValveSafetyMinimum = configuration.ReturnValveSafetyMinimum;
        for (int index = 0; index < 10; index++)
            _calibrationCurrents[index] = configuration.ReturnValveCalibration.Points[index].CurrentMilliamps;
        OnPropertyChanged(nameof(CalibrationCurrents));
        OnPropertyChanged(nameof(ValidatedCurvePoints));
    }

    private static bool HasValidationFailure(Exception exception) =>
        exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException;

    private void ReportException(Exception exception) => ValidationError = exception.Message;
}

internal static class ObservableCollectionExtensions
{
    internal static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source) => new(source);
}
