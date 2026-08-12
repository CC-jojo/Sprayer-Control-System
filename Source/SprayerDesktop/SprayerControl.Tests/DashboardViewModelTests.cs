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

        await dashboard.SetLeftMasterAsync(enabled: false);

        Assert.Empty(session.MaskWrites);
        Assert.Contains("firmware", dashboard.ErrorText, StringComparison.OrdinalIgnoreCase);
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
    public async Task MissingConfigurationAndSafetyViolationLockSetpointCommitBeforeSending()
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

        var configured = new DashboardViewModel(session, JsonHostConfigurationStoreTests.CreateConfiguration(), new LogViewModel());
        configured.PreviewReturnOpening = 10;
        await configured.CommitReturnOpeningAsync();
        Assert.Empty(session.Setpoints);
        Assert.Contains("safety", configured.ErrorText, StringComparison.OrdinalIgnoreCase);
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

    private static ControllerStatus Status(ushort outputMask = 0) =>
        new(0, 0, 0, 0, 0, 0, 0x0202, true, outputMask, false);

    private sealed class FakeSession(ControllerCapabilities? capabilities = null) : IControllerSession
    {
        public bool IsConnected { get; private set; } = true;
        public ControllerCapabilities Capabilities { get; } = capabilities ?? new(2, ControllerCapabilities.ExplicitNozzleMaskFeature);
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
