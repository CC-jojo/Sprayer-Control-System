using System.Runtime.CompilerServices;
using System.IO;
using SprayerControl.App.Services;
using SprayerControl.Core;

namespace SprayerControl.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private const string MissingCalibrationMessage = "比例阀尚未完成标定，请先在设置中完成十点标定";
    private static readonly ConditionalWeakTable<IControllerSession, SemaphoreSlim> SessionOperationGates = new();
    private readonly IControllerSession _session;
    private readonly LogViewModel _log;
    private readonly SemaphoreSlim _operationGate;
    private HostConfiguration? _configuration;
    private ControllerStatus? _latestStatus;
    private NozzleMask _confirmedMask;
    private NozzleMask _previewMask;
    private NozzleMask _preStartMask;
    private int _confirmedDuty;
    private int _previewDuty;
    private double _confirmedReturnOpening;
    private double _previewReturnOpening;
    private bool _isNozzleCommandPending;
    private bool _isSetpointCommandPending;
    private string _errorText = string.Empty;
    private bool _hasCommunicationFault;
    private bool _setpointInputInvalid;

    public DashboardViewModel(IControllerSession session, HostConfiguration? configuration, LogViewModel log)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _configuration = configuration;
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _operationGate = SessionOperationGates.GetValue(session, static _ => new SemaphoreSlim(1, 1));
        /* 占空比记忆：优先恢复上次使用的占空比，其次安全下限；
           并按当前固件/配置的 [下限, 上限] 钳制，避免记忆值越界导致后续所有设定点提交被拒。 */
        int dutyMinimum = configuration?.PwmDutyMinimum ?? 30;
        int dutyMaximum = Math.Max(dutyMinimum, _session.ParameterLimits.DutyMaximum);
        _confirmedDuty = Math.Clamp(configuration?.LastDutyPercent ?? dutyMinimum, dutyMinimum, dutyMaximum);
        _previewDuty = _confirmedDuty;
        _confirmedReturnOpening = configuration?.ReturnValveSafetyMinimum ?? 10;
        _previewReturnOpening = _confirmedReturnOpening;
        if (configuration is null)
            _errorText = MissingCalibrationMessage;
        RefreshCommand = new AsyncCommand(RefreshAsync, ReportException);
        CommitDutyCommand = new AsyncCommand(CommitDutyAsync, ReportException);
        CommitReturnOpeningCommand = new AsyncCommand(CommitReturnOpeningAsync, ReportException);
    }

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand CommitDutyCommand { get; }

    public AsyncCommand CommitReturnOpeningCommand { get; }

    public NozzleMask ConfirmedMask
    {
        get => _confirmedMask;
        private set => SetProperty(ref _confirmedMask, value);
    }

    public NozzleMask PreviewMask
    {
        get => _previewMask;
        private set => SetProperty(ref _previewMask, value);
    }

    /* 预启动掩码：电磁阀总开关关闭时本地选择，打开总开关后自动下发并清零。 */
    public NozzleMask PreStartMask
    {
        get => _preStartMask;
        private set => SetProperty(ref _preStartMask, value);
    }

    public bool IsPreStarting => LatestStatus?.PwmEnabled != true;

    public int PwmDutyMaximum => _session.ParameterLimits.DutyMaximum;

    public int ConfirmedDuty
    {
        get => _confirmedDuty;
        private set => SetProperty(ref _confirmedDuty, value);
    }

    public int PreviewDuty
    {
        get => _previewDuty;
        set => SetPreviewDuty(value);
    }

    public double ConfirmedReturnOpening
    {
        get => _confirmedReturnOpening;
        private set => SetProperty(ref _confirmedReturnOpening, value);
    }

    public double PreviewReturnOpening
    {
        get => _previewReturnOpening;
        set => SetPreviewReturnOpening(value);
    }

    public bool IsNozzleCommandPending
    {
        get => _isNozzleCommandPending;
        private set => SetProperty(ref _isNozzleCommandPending, value);
    }

    public bool IsSetpointCommandPending
    {
        get => _isSetpointCommandPending;
        private set => SetProperty(ref _isSetpointCommandPending, value);
    }

    public bool IsConnected => _session.IsConnected;

    public ControllerCapabilities Capabilities => _session.Capabilities;

    public ControllerStatus? LatestStatus
    {
        get => _latestStatus;
        private set => SetProperty(ref _latestStatus, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool HasCommunicationFault
    {
        get => _hasCommunicationFault;
        private set => SetProperty(ref _hasCommunicationFault, value);
    }

    public LogViewModel Log => _log;

    public void UpdateConfiguration(HostConfiguration? configuration)
    {
        _configuration = configuration;
        if (configuration is null)
            ErrorText = MissingCalibrationMessage;
        else if (ErrorText == MissingCalibrationMessage)
            ErrorText = string.Empty;
        if (configuration is not null && _previewReturnOpening < configuration.ReturnValveSafetyMinimum)
        {
            SetPreviewReturnOpeningValue(configuration.ReturnValveSafetyMinimum);
            _setpointInputInvalid = false;
        }

        OnPropertyChanged(nameof(CanCommitSetpoints));
        OnPropertyChanged(nameof(PwmDutyMinimum));
        OnPropertyChanged(nameof(SafetyMinimum));
        OnPropertyChanged(nameof(PreviewMappedCurrentMilliamps));
    }

    public bool CanCommitSetpoints => _configuration is not null;

    public int PwmDutyMinimum => _configuration?.PwmDutyMinimum ?? 30;

    public double SafetyMinimum => _configuration?.ReturnValveSafetyMinimum ?? 100;

    public double? PreviewMappedCurrentMilliamps
    {
        get
        {
            if (_configuration is null) return null;
            try { return _configuration.ReturnValveCalibration.CurrentMilliampsAt(PreviewReturnOpening, _configuration.ReturnValveSafetyMinimum); }
            catch (Exception) { return null; }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            try
            {
                await RefreshFromControllerAsync(cancellationToken);
                HasCommunicationFault = false;
                RestoreLocalStatusMessage();
            }
            finally
            {
                _operationGate.Release();
            }
        }
        catch (Exception exception)
        {
            ReportException(exception);
        }
    }

    /* 8 个喷头圆盘按钮：总开关关闭（预启动）时本地切换选中，打开时下发固件。 */
    public Task ToggleNozzleAsync(int group, bool right, CancellationToken cancellationToken = default)
    {
        if (IsPreStarting)
        {
            PreStartMask = PreStartMask.WithNozzle(group, right, !PreStartMask.IsEnabled(group, right));
            return Task.CompletedTask;
        }
        return SendMaskAsync(mask => mask.WithNozzle(group, right, !mask.IsEnabled(group, right)), cancellationToken);
    }

    /* 总开关切换：
       - 打开：预启动掩码若有选中则先下发再开总开关，随后清空预启动；
       - 关闭：清空预启动、固件掩码置全关，再关总开关。 */
    public async Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await RunSetpointOperationAsync(async token =>
        {
            if (enabled)
            {
                NozzleMask preStart = PreStartMask;
                if (preStart.Value != 0)
                {
                    await _session.SetNozzleMaskAsync(preStart, token);
                }
                await _session.SetPwmEnabledAsync(true, token);
                PreStartMask = NozzleMask.None;
                _log.Add(preStart.Value != 0
                    ? "电磁阀总开关已开启，预启动喷头掩码已生效。"
                    : "电磁阀总开关已开启。");
            }
            else
            {
                PreStartMask = NozzleMask.None;
                await _session.SetNozzleMaskAsync(NozzleMask.None, token);
                await _session.SetPwmEnabledAsync(false, token);
                _log.Add("电磁阀总开关已关闭，所有喷头已关闭。");
            }
            await RefreshFromControllerAsync(token);
        }, cancellationToken);
    }

    public void SetPreviewDuty(int duty)
    {
        if (_configuration is null)
        {
            RejectSetpointInput(MissingCalibrationMessage);
            return;
        }

        ushort dutyMaximum = _session.ParameterLimits.DutyMaximum;
        if (duty < _configuration.PwmDutyMinimum || duty > dutyMaximum)
        {
            RejectSetpointInput($"电磁阀占空比必须在 {_configuration.PwmDutyMinimum}% 到 {dutyMaximum}% 之间。");
            return;
        }

        SetPreviewDutyValue(duty);
        _setpointInputInvalid = false;
        ErrorText = string.Empty;
    }

    public void SetPreviewReturnOpening(double opening)
    {
        if (_configuration is null)
        {
            RejectSetpointInput(MissingCalibrationMessage);
            return;
        }

        if (!double.IsFinite(opening) || opening > 100)
        {
            RejectSetpointInput("回流阀开度必须在 0% 到 100% 之间。");
            return;
        }

        /* 低于安全下限时自动钳制到安全下限，操作始终可用；进度条停在安全下限处（橙色安全区顶端）。 */
        double clamped = Math.Max(opening, _configuration.ReturnValveSafetyMinimum);
        if (clamped > opening)
        {
            _log.Add($"回流阀开度已自动钳制到安全下限 {_configuration.ReturnValveSafetyMinimum:F0}%。");
        }

        SetPreviewReturnOpeningValue(clamped);
        _setpointInputInvalid = false;
        ErrorText = string.Empty;
    }

    public Task CommitDutyAsync(CancellationToken cancellationToken = default) => CommitSetpointsAsync(cancellationToken);

    public Task CommitReturnOpeningAsync(CancellationToken cancellationToken = default) => CommitSetpointsAsync(cancellationToken);

    private async Task SendMaskAsync(Func<NozzleMask, NozzleMask> deriveTarget, CancellationToken cancellationToken)
    {
        if (!Capabilities.SupportsExplicitNozzleMask)
        {
            PreviewMask = ConfirmedMask;
            ReportException(new NotSupportedException("控制器固件不支持独立的八喷头掩码。"));
            return;
        }

        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            IsNozzleCommandPending = true;
            RestoreLocalStatusMessage();
            try
            {
                NozzleMask target = deriveTarget(ConfirmedMask);
                PreviewMask = target;
                await _session.SetNozzleMaskAsync(target, cancellationToken);
                await RefreshFromControllerAsync(cancellationToken);
                PreviewMask = ConfirmedMask;
                _log.Add("喷头掩码已由控制器确认。");
            }
            finally
            {
                IsNozzleCommandPending = false;
                _operationGate.Release();
            }
        }
        catch (Exception exception)
        {
            PreviewMask = ConfirmedMask;
            ReportException(exception);
        }
    }

    private async Task CommitSetpointsAsync(CancellationToken cancellationToken)
    {
        int? priorDuty = null;
        double? priorOpening = null;
        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            IsSetpointCommandPending = true;
            ErrorText = string.Empty;
            try
            {
                HostConfiguration? configuration = _configuration;
                int dutySnapshot = PreviewDuty;
                double openingSnapshot = PreviewReturnOpening;
                priorDuty = ConfirmedDuty;
                priorOpening = ConfirmedReturnOpening;
                if (configuration is null)
                {
                    RejectSetpointInput(MissingCalibrationMessage);
                    return;
                }

                ushort dutyMaximum = _session.ParameterLimits.DutyMaximum;
                if (_setpointInputInvalid)
                {
                    RejectSetpointInput("设定值预览无效，请重新调整。");
                    return;
                }

                if (dutySnapshot < configuration.PwmDutyMinimum || dutySnapshot > dutyMaximum)
                {
                    RejectSetpointInput($"电磁阀占空比必须在 {configuration.PwmDutyMinimum}% 到 {dutyMaximum}% 之间。");
                    return;
                }

                if (openingSnapshot < configuration.ReturnValveSafetyMinimum || openingSnapshot > 100)
                {
                    RejectSetpointInput($"回流阀开度必须在 {configuration.ReturnValveSafetyMinimum}% 到 100% 之间（安全限制）。");
                    return;
                }

                double milliamps = configuration.ReturnValveCalibration.CurrentMilliampsAt(
                    openingSnapshot,
                    configuration.ReturnValveSafetyMinimum);
                ushort currentMicroamps = checked((ushort)Math.Round(milliamps * 1000, MidpointRounding.AwayFromZero));
                await _session.WriteOperatingSetpointsAsync((ushort)dutySnapshot, currentMicroamps, cancellationToken);
                await RefreshFromControllerAsync(cancellationToken);
                ConfirmedDuty = dutySnapshot;
                ConfirmedReturnOpening = openingSnapshot;
                _log.Add("电磁阀占空比与回流阀开度已由控制器确认。");
            }
            finally
            {
                IsSetpointCommandPending = false;
                _operationGate.Release();
            }
        }
        catch (Exception exception)
        {
            if (priorDuty.HasValue)
            {
                SetPreviewDutyValue(priorDuty.Value);
                SetPreviewReturnOpeningValue(priorOpening!.Value);
            }

            ReportException(exception);
        }
    }

    private async Task RunSetpointOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken,
        int? priorDuty = null,
        double? priorOpening = null)
    {
        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            IsSetpointCommandPending = true;
            RestoreLocalStatusMessage();
            try
            {
                await operation(cancellationToken);
            }
            finally
            {
                IsSetpointCommandPending = false;
                _operationGate.Release();
            }
        }
        catch (Exception exception)
        {
            if (priorDuty.HasValue)
            {
                SetPreviewDutyValue(priorDuty.Value);
                SetPreviewReturnOpeningValue(priorOpening!.Value);
            }

            ReportException(exception);
        }
    }

    private async Task RefreshFromControllerAsync(CancellationToken cancellationToken)
    {
        ControllerStatus status = await _session.ReadStatusAsync(cancellationToken);
        LatestStatus = status;
        ConfirmedMask = new NozzleMask((byte)status.OutputMask);
        PreviewMask = ConfirmedMask;
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsPreStarting));
    }

    private void RejectSetpointInput(string message)
    {
        _setpointInputInvalid = true;
        ErrorText = message;
        _log.Add(message);
    }

    private void ReportException(Exception exception)
    {
        HasCommunicationFault = exception is IOException or TimeoutException or ModbusProtocolException or ObjectDisposedException;
        ErrorText = exception.Message;
        _log.Add(exception.Message);
    }

    private void RestoreLocalStatusMessage() => ErrorText = _configuration is null ? MissingCalibrationMessage : string.Empty;

    private void SetPreviewDutyValue(int value) => SetProperty(ref _previewDuty, value, nameof(PreviewDuty));

    private void SetPreviewReturnOpeningValue(double value) =>
        SetPreviewReturnOpeningAndNotify(value);

    private void SetPreviewReturnOpeningAndNotify(double value)
    {
        if (SetProperty(ref _previewReturnOpening, value, nameof(PreviewReturnOpening)))
            OnPropertyChanged(nameof(PreviewMappedCurrentMilliamps));
    }
}
