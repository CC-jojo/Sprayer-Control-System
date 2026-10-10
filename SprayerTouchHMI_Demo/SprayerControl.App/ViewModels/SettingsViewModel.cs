using System.Collections.ObjectModel;
using SprayerControl.App.Services;
using SprayerControl.Core;

namespace SprayerControl.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IHostConfigurationStore _store;
    private readonly bool _allowPersistence;
    private readonly ObservableCollection<double?> _calibrationCurrents = Enumerable.Repeat<double?>(null, 10).ToObservableCollection();
    private readonly ReadOnlyObservableCollection<double?> _readOnlyCalibrationCurrents;
    private readonly List<NozzleMappingEntry> _nozzleMappingEntries = [];
    private int _pwmDutyMinimum = 30;
    private int _pwmDutyMaximum = 80;
    private int _returnValveSafetyMinimum = 10;
    private int? _lastDutyPercent;
    private bool _powerOnReturnValveEnabled;
    private int _powerOnReturnValvePercent = 100;
    private double? _lastReturnOpeningPercent;
    private string _validationError = string.Empty;
    private HostConfiguration? _savedConfiguration;

    public SettingsViewModel(IHostConfigurationStore store, HostConfiguration? configuration = null, bool allowPersistence = true)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _allowPersistence = allowPersistence;
        _readOnlyCalibrationCurrents = new ReadOnlyObservableCollection<double?>(_calibrationCurrents);
        if (configuration is not null)
            CopyFrom(configuration);
        else
            ResetNozzleMapping();
        SaveCommand = new AsyncCommand(async token => await SaveAsync(token), ReportException);
    }

    public AsyncCommand SaveCommand { get; }

    public bool CanSaveLocal => _allowPersistence;

    public int PwmDutyMinimum
    {
        get => _pwmDutyMinimum;
        set => SetProperty(ref _pwmDutyMinimum, value);
    }

    /* 占空比上限来自控制器能力区；连接后由 MainWindow 同步固件值。 */
    public int PwmDutyMaximum
    {
        get => _pwmDutyMaximum;
        set => SetProperty(ref _pwmDutyMaximum, value);
    }

    public int ReturnValveSafetyMinimum
    {
        get => _returnValveSafetyMinimum;
        set => SetProperty(ref _returnValveSafetyMinimum, value);
    }

    /* 占空比记忆：程序退出时由主窗口写入当前占空比，启动时恢复。 */
    public int? LastDutyPercent
    {
        get => _lastDutyPercent;
        set => SetProperty(ref _lastDutyPercent, value);
    }

    /* 上电默认回流阀开关：打开时，控制器上电与上位机启动都把回流阀调到
       PowerOnReturnValvePercent；关闭时改为继承 LastReturnOpeningPercent。默认关闭。 */
    public bool PowerOnReturnValveEnabled
    {
        get => _powerOnReturnValveEnabled;
        set => SetProperty(ref _powerOnReturnValveEnabled, value);
    }

    /* 上电默认回流阀开度：受安全下限约束（不低于下限、不高于 100）。 */
    public int PowerOnReturnValvePercent
    {
        get => _powerOnReturnValvePercent;
        set => SetProperty(ref _powerOnReturnValvePercent, value);
    }

    /* 回流阀开度记忆：程序退出时由主窗口写入，开关关闭时用于启动恢复。 */
    public double? LastReturnOpeningPercent
    {
        get => _lastReturnOpeningPercent;
        set => SetProperty(ref _lastReturnOpeningPercent, value);
    }

    public ReadOnlyObservableCollection<double?> CalibrationCurrents => _readOnlyCalibrationCurrents;

    /* 十点标定是否已全部填满：填满即触发自动保存，防止重启后丢失。 */
    public bool IsCalibrationComplete => _calibrationCurrents.All(static current => current.HasValue);

    public IReadOnlyList<NozzleMappingEntry> NozzleMappingEntries => _nozzleMappingEntries;

    public void SetNozzleMappingSelection(int entryIndex, int physicalIndex)
    {
        if (entryIndex is >= 0 and < 8 && physicalIndex is >= 0 and <= 7)
            _nozzleMappingEntries[entryIndex].SelectedPhysicalIndex = physicalIndex;
    }

    public void ResetNozzleMapping()
    {
        byte[] mapping = NozzleMask.DefaultUiToPhysicalBit;
        if (_nozzleMappingEntries.Count == 0)
        {
            for (int index = 0; index < 8; index++)
                _nozzleMappingEntries.Add(new NozzleMappingEntry(NozzleMappingEntry.UiLabels[index], mapping[index]));
        }
        else
        {
            for (int index = 0; index < 8; index++)
                _nozzleMappingEntries[index].SelectedPhysicalIndex = mapping[index];
        }

        OnPropertyChanged(nameof(NozzleMappingEntries));
    }

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
            throw new ArgumentOutOfRangeException(nameof(openingPercent), "标定开度必须是 10 到 100，且以 10 递增。");

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
        if (!_allowPersistence)
        {
            ValidationError = "演示模式禁止写入本地配置。";
            return false;
        }

        HostConfiguration configuration;
        try
        {
            configuration = new HostConfiguration(
                schemaVersion: 1,
                pwmDutyMinimum: PwmDutyMinimum,
                returnValveSafetyMinimum: ReturnValveSafetyMinimum,
                returnValveCalibration: BuildCalibration(),
                lastModifiedUtc: DateTimeOffset.UtcNow,
                nozzleMapping: BuildNozzleMapping(),
                lastDutyPercent: LastDutyPercent,
                powerOnReturnValveEnabled: PowerOnReturnValveEnabled,
                powerOnReturnValvePercent: PowerOnReturnValvePercent,
                lastReturnOpeningPercent: LastReturnOpeningPercent);
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
            throw new ArgumentException("需要填写全部十个标定电流。");

        CalibrationPoint[] points = Enumerable.Range(1, 10)
            .Select(index => new CalibrationPoint(index * 10, _calibrationCurrents[index - 1]!.Value))
            .ToArray();
        return new ReturnValveCalibration(points);
    }

    private byte[] BuildNozzleMapping()
    {
        if (_nozzleMappingEntries.Count != 8)
            return (byte[])NozzleMask.DefaultUiToPhysicalBit.Clone();
        byte[] mapping = _nozzleMappingEntries.Select(entry => (byte)entry.SelectedPhysicalIndex).ToArray();
        return NozzleMask.ValidateMapping(mapping);
    }

    private void CopyFrom(HostConfiguration configuration)
    {
        PwmDutyMinimum = configuration.PwmDutyMinimum;
        ReturnValveSafetyMinimum = configuration.ReturnValveSafetyMinimum;
        LastDutyPercent = configuration.LastDutyPercent;
        PowerOnReturnValveEnabled = configuration.PowerOnReturnValveEnabled;
        PowerOnReturnValvePercent = configuration.PowerOnReturnValvePercent;
        LastReturnOpeningPercent = configuration.LastReturnOpeningPercent;
        for (int index = 0; index < 10; index++)
            _calibrationCurrents[index] = configuration.ReturnValveCalibration.Points[index].CurrentMilliamps;
        _nozzleMappingEntries.Clear();
        byte[] mapping = configuration.NozzleMapping;
        for (int index = 0; index < 8; index++)
            _nozzleMappingEntries.Add(new NozzleMappingEntry(NozzleMappingEntry.UiLabels[index], mapping[index]));
        OnPropertyChanged(nameof(CalibrationCurrents));
        OnPropertyChanged(nameof(ValidatedCurvePoints));
        OnPropertyChanged(nameof(NozzleMappingEntries));
    }

    private static bool HasValidationFailure(Exception exception) =>
        exception is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException;

    private void ReportException(Exception exception) => ValidationError = exception.Message;
}

internal static class ObservableCollectionExtensions
{
    internal static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> source) => new(source);
}

/// <summary>一条界面开关→物理电磁阀的映射记录。</summary>
public sealed class NozzleMappingEntry
{
    /* 物理喷头标签：bit0=左一、bit1=右一…bit7=右四，索引即物理位。 */
    public static readonly string[] PhysicalLabels = ["左一", "右一", "左二", "右二", "左三", "右三", "左四", "右四"];

    /* 界面开关标签：与界面位顺序一致。 */
    public static readonly string[] UiLabels = ["左一", "右一", "左二", "右二", "左三", "右三", "左四", "右四"];

    public NozzleMappingEntry(string uiLabel, int selectedPhysicalIndex)
    {
        UiLabel = uiLabel;
        SelectedPhysicalIndex = selectedPhysicalIndex;
    }

    public string UiLabel { get; }

    public int SelectedPhysicalIndex { get; set; }
}
