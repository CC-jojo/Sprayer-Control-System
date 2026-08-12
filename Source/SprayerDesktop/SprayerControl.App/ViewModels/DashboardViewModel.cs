using System.Runtime.CompilerServices;
using SprayerControl.App.Services;
using SprayerControl.Core;

namespace SprayerControl.App.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private static readonly ConditionalWeakTable<IControllerSession, SemaphoreSlim> SessionOperationGates = new();
    private readonly IControllerSession _session;
    private readonly LogViewModel _log;
    private readonly SemaphoreSlim _operationGate;
    private HostConfiguration? _configuration;
    private ControllerStatus? _latestStatus;
    private NozzleMask _confirmedMask;
    private NozzleMask _previewMask;
    private int _confirmedDuty;
    private int _previewDuty;
    private double _confirmedReturnOpening;
    private double _previewReturnOpening;
    private bool _isNozzleCommandPending;
    private bool _isSetpointCommandPending;
    private string _errorText = string.Empty;
    private bool _setpointInputInvalid;

    public DashboardViewModel(IControllerSession session, HostConfiguration? configuration, LogViewModel log)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _configuration = configuration;
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _operationGate = SessionOperationGates.GetValue(session, static _ => new SemaphoreSlim(1, 1));
        _confirmedDuty = configuration?.PwmDutyMinimum ?? 30;
        _previewDuty = _confirmedDuty;
        _confirmedReturnOpening = configuration?.ReturnValveSafetyMinimum ?? 10;
        _previewReturnOpening = _confirmedReturnOpening;
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

    public LogViewModel Log => _log;

    public void UpdateConfiguration(HostConfiguration? configuration)
    {
        _configuration = configuration;
        if (configuration is not null && _previewReturnOpening < configuration.ReturnValveSafetyMinimum)
        {
            SetPreviewReturnOpeningValue(configuration.ReturnValveSafetyMinimum);
            _setpointInputInvalid = false;
        }

        OnPropertyChanged(nameof(CanCommitSetpoints));
    }

    public bool CanCommitSetpoints => _configuration is not null;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            try
            {
                await RefreshFromControllerAsync(cancellationToken);
                ErrorText = string.Empty;
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

    public Task ToggleNozzleAsync(int group, bool right, CancellationToken cancellationToken = default) =>
        SendMaskAsync(mask => mask.WithNozzle(group, right, !mask.IsEnabled(group, right)), cancellationToken);

    public Task SetLeftMasterAsync(bool enabled, CancellationToken cancellationToken = default) =>
        SendMaskAsync(mask => mask.WithLeft(enabled), cancellationToken);

    public Task SetRightMasterAsync(bool enabled, CancellationToken cancellationToken = default) =>
        SendMaskAsync(mask => mask.WithRight(enabled), cancellationToken);

    public Task ApplyCanopyPresetAsync(CanopyPreset preset, CancellationToken cancellationToken = default) =>
        SendMaskAsync(mask => NozzleMask.ForPreset(
            preset,
            leftEnabled: (mask.Value & 0x55) != 0,
            rightEnabled: (mask.Value & 0xAA) != 0), cancellationToken);

    public async Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        await RunSetpointOperationAsync(async token =>
        {
            await _session.SetPwmEnabledAsync(enabled, token);
            await RefreshFromControllerAsync(token);
        }, cancellationToken);
    }

    public void SetPreviewDuty(int duty)
    {
        if (_configuration is null)
        {
            RejectSetpointInput("Return-valve calibration configuration is required before changing setpoints.");
            return;
        }

        if (duty < _configuration.PwmDutyMinimum || duty > 80)
        {
            RejectSetpointInput($"PWM duty must be from {_configuration.PwmDutyMinimum} through 80 percent.");
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
            RejectSetpointInput("Return-valve calibration configuration is required before changing setpoints.");
            return;
        }

        if (!double.IsFinite(opening) || opening < _configuration.ReturnValveSafetyMinimum || opening > 100)
        {
            RejectSetpointInput($"Return opening must be from {_configuration.ReturnValveSafetyMinimum} through 100 percent for safety.");
            return;
        }

        SetPreviewReturnOpeningValue(opening);
        _setpointInputInvalid = false;
        ErrorText = string.Empty;
    }

    public Task CommitDutyAsync(CancellationToken cancellationToken = default) => CommitSetpointsAsync(cancellationToken);

    public Task CommitReturnOpeningAsync(CancellationToken cancellationToken = default) => CommitSetpointsAsync(cancellationToken);

    public bool LeftEnabled => (ConfirmedMask.Value & 0x55) != 0;

    public bool RightEnabled => (ConfirmedMask.Value & 0xAA) != 0;

    private async Task SendMaskAsync(Func<NozzleMask, NozzleMask> deriveTarget, CancellationToken cancellationToken)
    {
        if (!Capabilities.SupportsExplicitNozzleMask)
        {
            PreviewMask = ConfirmedMask;
            ReportException(new NotSupportedException("This controller firmware does not support explicit nozzle masks."));
            return;
        }

        try
        {
            await _operationGate.WaitAsync(cancellationToken);
            IsNozzleCommandPending = true;
            ErrorText = string.Empty;
            try
            {
                NozzleMask target = deriveTarget(ConfirmedMask);
                PreviewMask = target;
                await _session.SetNozzleMaskAsync(target, cancellationToken);
                await RefreshFromControllerAsync(cancellationToken);
                PreviewMask = ConfirmedMask;
                _log.Add("Nozzle mask confirmed by controller.");
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
                    RejectSetpointInput("Return-valve calibration configuration is required before committing setpoints.");
                    return;
                }

                if (_setpointInputInvalid || dutySnapshot < configuration.PwmDutyMinimum || dutySnapshot > 80 ||
                    openingSnapshot < configuration.ReturnValveSafetyMinimum || openingSnapshot > 100)
                {
                    RejectSetpointInput("Setpoint preview is outside the configured safety range.");
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
                _log.Add("PWM duty and return-valve opening confirmed by controller.");
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
            ErrorText = string.Empty;
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
        OnPropertyChanged(nameof(LeftEnabled));
        OnPropertyChanged(nameof(RightEnabled));
    }

    private void RejectSetpointInput(string message)
    {
        _setpointInputInvalid = true;
        ErrorText = message;
        _log.Add(message);
    }

    private void ReportException(Exception exception)
    {
        ErrorText = exception.Message;
        _log.Add(exception.Message);
    }

    private void SetPreviewDutyValue(int value) => SetProperty(ref _previewDuty, value, nameof(PreviewDuty));

    private void SetPreviewReturnOpeningValue(double value) =>
        SetProperty(ref _previewReturnOpening, value, nameof(PreviewReturnOpening));
}
