using SprayerControl.App.Services;
using SprayerControl.App.ViewModels;
using SprayerControl.Core;

namespace SprayerControl.Tests;

public sealed class DashboardViewModelTests
{
    [Fact]
    public async Task RefreshUsesControllerOutputMaskAsTheConfirmedState()
    {
        var session = new FakeSession { Status = Status(outputMask: 0xA5) };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());

        await dashboard.RefreshAsync();

        Assert.Equal((byte)0xA5, dashboard.ConfirmedMask.Value);
        Assert.Equal(dashboard.ConfirmedMask, dashboard.PreviewMask);
        Assert.Same(session.Status, dashboard.LatestStatus);
    }

    [Fact]
    public async Task NozzleOperationSendsOnceThenConfirmsFromStatus()
    {
        var session = new FakeSession { Status = Status(outputMask: 0x01) };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();
        session.Status = Status(outputMask: 0x03);

        await dashboard.ToggleNozzleAsync(group: 1, right: true);

        Assert.Equal([new NozzleMask(0x03)], session.MaskWrites);
        Assert.Equal(2, session.ReadCount);
        Assert.Equal((byte)0x03, dashboard.ConfirmedMask.Value);
        Assert.False(dashboard.IsNozzleCommandPending);
    }

    [Fact]
    public async Task LegacySessionRejectsExplicitMaskWithoutSending()
    {
        var session = new FakeSession(new ControllerCapabilities(1, 0)) { Status = Status(outputMask: 0x01) };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();

        await dashboard.ToggleNozzleAsync(group: 1, right: true);

        Assert.Empty(session.MaskWrites);
        Assert.Contains("固件", dashboard.ErrorText);
    }

    [Fact]
    public async Task MaskFailureRollsPreviewBackAndRecordsTheError()
    {
        var session = new FakeSession { Status = Status(outputMask: 0x01), NextFailure = new IOException("lost controller") };
        var log = new LogViewModel();
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), log);
        await dashboard.RefreshAsync();

        await dashboard.ToggleNozzleAsync(group: 1, right: true);

        Assert.Equal((byte)0x01, dashboard.PreviewMask.Value);
        Assert.Equal((byte)0x01, dashboard.ConfirmedMask.Value);
        Assert.Contains("lost controller", dashboard.ErrorText);
        Assert.True(dashboard.HasCommunicationFault);
        Assert.Contains(log.Entries, entry => entry.Contains("lost controller"));
    }

    [Fact]
    public async Task PreviewDoesNotSendAndCommitMapsOpeningToMicroampsOnce()
    {
        var session = new FakeSession { Status = Status() };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());

        dashboard.PreviewDuty = 45;
        dashboard.PreviewReturnOpening = 35;
        Assert.Empty(session.Setpoints);
        await dashboard.CommitReturnOpeningAsync();

        Assert.Equal(((ushort)45, (ushort)350), Assert.Single(session.Setpoints));
        Assert.Equal(45, dashboard.ConfirmedDuty);
        Assert.Equal(35, dashboard.ConfirmedReturnOpening);
        Assert.Equal(0.35, dashboard.PreviewMappedCurrentMilliamps!.Value, 6);
    }

    [Fact]
    public async Task MissingConfigurationBlocksCommitBeforeSending()
    {
        var session = new FakeSession { Status = Status() };
        var dashboard = new DashboardViewModel(session, null, new LogViewModel());
        await dashboard.RefreshAsync();
        dashboard.PreviewDuty = 45;
        await dashboard.CommitDutyAsync();
        Assert.Empty(session.Setpoints);
        Assert.Equal("比例阀尚未完成标定，请先在设置中完成十点标定", dashboard.ErrorText);
        Assert.False(dashboard.HasCommunicationFault);
        Assert.False(dashboard.CanCommitSetpoints);
        Assert.True(dashboard.IsConnected);
    }

    [Fact]
    public async Task OpeningBelowSafetyMinimumIsClampedAndCommittedAtTheSafetyFloor()
    {
        var session = new FakeSession { Status = Status() };
        // 默认配置安全下限为 20%。
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        dashboard.PreviewDuty = 45;

        dashboard.PreviewReturnOpening = 10;
        Assert.Equal(20, dashboard.PreviewReturnOpening);
        Assert.Contains(dashboard.Log.Entries, entry => entry.Contains("自动钳制"));
        Assert.Empty(dashboard.ErrorText);

        await dashboard.CommitReturnOpeningAsync();
        Assert.Equal((ushort)200, Assert.Single(session.Setpoints).Current);
        Assert.Equal(20, dashboard.ConfirmedReturnOpening);
    }

    [Fact]
    public async Task QueuedNozzleActionsComposeFromTheLatestConfirmedMaskInsideTheSharedGate()
    {
        var session = new QueuedSession();
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();

        Task left = dashboard.ToggleNozzleAsync(group: 1, right: false);
        await session.FirstMaskWriteStarted.Task;
        Task right = dashboard.ToggleNozzleAsync(group: 1, right: true);
        session.ReleaseFirstMaskWrite.SetResult();
        await Task.WhenAll(left, right);

        Assert.Equal([new NozzleMask(0x01), new NozzleMask(0x03)], session.MaskWrites);
        Assert.Equal(3, session.ReadCount);
        Assert.Equal((byte)0x03, dashboard.ConfirmedMask.Value);
        Assert.Equal(dashboard.ConfirmedMask, dashboard.PreviewMask);
    }

    [Fact]
    public async Task QueuedSetpointCommitUsesOneGateSnapshotAndDoesNotPromoteALaterPreview()
    {
        var session = new QueuedSession();
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();

        Task blockingPwm = dashboard.SetPwmEnabledAsync(true);
        await session.PwmWriteStarted.Task;
        dashboard.PreviewDuty = 45;
        dashboard.PreviewReturnOpening = 35;
        Task commit = dashboard.CommitDutyAsync();
        dashboard.PreviewDuty = 46;
        dashboard.PreviewReturnOpening = 36;
        session.ReleasePwmWrite.SetResult();
        await session.SetpointWriteStarted.Task;
        dashboard.PreviewDuty = 50;
        dashboard.PreviewReturnOpening = 50;
        session.ReleaseSetpointWrite.SetResult();
        await Task.WhenAll(blockingPwm, commit);

        Assert.Equal(((ushort)46, (ushort)360), Assert.Single(session.Setpoints));
        Assert.Equal(46, dashboard.ConfirmedDuty);
        Assert.Equal(36, dashboard.ConfirmedReturnOpening);
        Assert.Equal(50, dashboard.PreviewDuty);
        Assert.Equal(50, dashboard.PreviewReturnOpening);
    }

    [Fact]
    public void ConfirmedDutyStartsWithRememberedDutyWhenConfigured()
    {
        HostConfiguration configuration = new(
            1, 30, 20,
            new ReturnValveCalibration(JsonHostConfigurationStoreTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            lastDutyPercent: 71);
        var dashboard = new DashboardViewModel(new FakeSession(), configuration, new LogViewModel());

        Assert.Equal(71, dashboard.ConfirmedDuty);
        Assert.Equal(71, dashboard.PreviewDuty);
    }

    [Fact]
    public void RememberedDutyAboveFirmwareLimitIsClampedSoCommitsNeverFail()
    {
        // 记忆占空比 90 超过 FakeSession 固件上限 80：必须钳制，否则所有设定点提交会被校验拒绝。
        HostConfiguration configuration = new(
            1, 30, 20,
            new ReturnValveCalibration(JsonHostConfigurationStoreTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            lastDutyPercent: 90);
        var session = new FakeSession { Status = Status(outputMask: 0x01) };
        var dashboard = new DashboardViewModel(session, configuration, new LogViewModel());

        Assert.Equal(80, dashboard.ConfirmedDuty);
        Assert.Equal(80, dashboard.PreviewDuty);
    }

    [Fact]
    public void RememberedDutyBelowConfigurationMinimumIsClamped()
    {
        HostConfiguration configuration = new(
            1, 45, 20,
            new ReturnValveCalibration(JsonHostConfigurationStoreTests.ValidPoints()),
            DateTimeOffset.UtcNow,
            lastDutyPercent: 25);
        var dashboard = new DashboardViewModel(new FakeSession(), configuration, new LogViewModel());

        Assert.Equal(45, dashboard.ConfirmedDuty);
        Assert.Equal(45, dashboard.PreviewDuty);
    }

    [Fact]
    public async Task NozzleToggleWhilePwmOffUpdatesPreStartLocallyWithoutSending()
    {
        var session = new FakeSession { Status = Status(pwmEnabled: false) };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();

        Assert.True(dashboard.IsPreStarting);
        await dashboard.ToggleNozzleAsync(group: 1, right: false);

        Assert.Empty(session.MaskWrites);
        Assert.Equal((byte)0x01, dashboard.PreStartMask.Value);
        Assert.Equal((byte)0x00, dashboard.ConfirmedMask.Value);

        await dashboard.ToggleNozzleAsync(group: 1, right: false);
        Assert.Equal((byte)0x00, dashboard.PreStartMask.Value);

        await dashboard.ToggleNozzleAsync(group: 2, right: true);
        Assert.Equal((byte)0x08, dashboard.PreStartMask.Value);
    }

    [Fact]
    public async Task TurningPwmOnAppliesPreStartMaskThenClearsIt()
    {
        var session = new FakeSession { Status = Status(pwmEnabled: false) };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();

        // 通过 8 个圆盘逐一切换组合出"第3+4组双侧"=0xF0。
        await dashboard.ToggleNozzleAsync(group: 3, right: false);
        await dashboard.ToggleNozzleAsync(group: 3, right: true);
        await dashboard.ToggleNozzleAsync(group: 4, right: false);
        await dashboard.ToggleNozzleAsync(group: 4, right: true);
        Assert.Equal((byte)0xF0, dashboard.PreStartMask.Value);
        Assert.Empty(session.MaskWrites);

        await dashboard.SetPwmEnabledAsync(true);

        // 预启动掩码被下发（FakeSession 不模拟读回，ConfirmedMask 仍为 0）。
        Assert.Equal(new NozzleMask(0xF0), Assert.Single(session.MaskWrites));
        Assert.Equal((byte)0x00, dashboard.PreStartMask.Value);
        Assert.False(dashboard.IsPreStarting);
    }

    [Fact]
    public async Task TurningPwmOffClearsPreStartAndSendsAllOffMask()
    {
        var session = new FakeSession { Status = Status(outputMask: 0x03) };
        var dashboard = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        await dashboard.RefreshAsync();

        await dashboard.SetPwmEnabledAsync(false);

        Assert.Equal(new NozzleMask(0x00), Assert.Single(session.MaskWrites));
        Assert.Equal((byte)0x00, dashboard.PreStartMask.Value);
        Assert.True(dashboard.IsPreStarting);
    }

    private static ControllerStatus Status(ushort outputMask = 0, bool pwmEnabled = true) =>
        new(0, 0, 0, 0, 0, 0, 0x0202, pwmEnabled, outputMask, false);

    private sealed class FakeSession(ControllerCapabilities? capabilities = null) : IControllerSession
    {
        public bool IsConnected { get; private set; } = true;
        public ControllerCapabilities Capabilities { get; } = capabilities ?? new(2, ControllerCapabilities.ExplicitNozzleMaskFeature);
        public ControllerParameterLimits ParameterLimits { get; } = ControllerParameterLimits.Default;
        public ControllerStatus Status { get; set; } = Status();
        public Exception? NextFailure { get; set; }
        public int ReadCount { get; private set; }
        public List<NozzleMask> MaskWrites { get; } = [];
        public List<(ushort Duty, ushort Current)> Setpoints { get; } = [];

        public Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default)
        {
            ThrowNext();
            MaskWrites.Add(mask);
            return Task.CompletedTask;
        }

        public Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            ThrowNext();
            Status = Status(pwmEnabled: enabled, outputMask: enabled ? Status.OutputMask : (ushort)0);
            return Task.CompletedTask;
        }

        public Task WriteOperatingSetpointsAsync(ushort duty, ushort targetCurrentMicroamps, CancellationToken cancellationToken = default)
        {
            ThrowNext();
            Setpoints.Add((duty, targetCurrentMicroamps));
            return Task.CompletedTask;
        }

        public Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(Status);
        }

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }

        private void ThrowNext()
        {
            Exception? failure = NextFailure;
            NextFailure = null;
            if (failure is not null)
                throw failure;
        }
    }

    private sealed class QueuedSession : IControllerSession
    {
        public TaskCompletionSource FirstMaskWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstMaskWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource PwmWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleasePwmWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SetpointWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSetpointWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsConnected => true;
        public ControllerCapabilities Capabilities { get; } = new(2, ControllerCapabilities.ExplicitNozzleMaskFeature);
        public ControllerParameterLimits ParameterLimits { get; } = ControllerParameterLimits.Default;
        public ControllerStatus Status { get; private set; } = Status();
        public List<NozzleMask> MaskWrites { get; } = [];
        public List<(ushort Duty, ushort Current)> Setpoints { get; } = [];
        public int ReadCount { get; private set; }

        public async Task SetNozzleMaskAsync(NozzleMask mask, CancellationToken cancellationToken = default)
        {
            MaskWrites.Add(mask);
            if (MaskWrites.Count == 1)
            {
                FirstMaskWriteStarted.TrySetResult();
                await ReleaseFirstMaskWrite.Task.WaitAsync(cancellationToken);
            }

            Status = Status(mask.Value);
        }

        public async Task SetPwmEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        {
            PwmWriteStarted.TrySetResult();
            await ReleasePwmWrite.Task.WaitAsync(cancellationToken);
        }

        public async Task WriteOperatingSetpointsAsync(ushort duty, ushort targetCurrentMicroamps, CancellationToken cancellationToken = default)
        {
            Setpoints.Add((duty, targetCurrentMicroamps));
            SetpointWriteStarted.TrySetResult();
            await ReleaseSetpointWrite.Task.WaitAsync(cancellationToken);
        }

        public Task<ControllerStatus> ReadStatusAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(Status);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

}
