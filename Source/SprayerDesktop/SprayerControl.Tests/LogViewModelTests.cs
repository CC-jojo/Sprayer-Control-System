using System.ComponentModel;
using System.Windows.Input;
using SprayerControl.App.ViewModels;

namespace SprayerControl.Tests;

public sealed class LogViewModelTests
{
    [Fact]
    public void RecentViewKeepsNewestFiveWithoutDroppingFullHistory()
    {
        var log = new LogViewModel();
        foreach (int value in Enumerable.Range(1, 6))
            log.Add($"entry-{value}");

        Assert.Equal(6, log.Entries.Count);
        Assert.Equal(["entry-2", "entry-3", "entry-4", "entry-5", "entry-6"], log.RecentEntries);
        log.Clear();
        Assert.Empty(log.Entries);
        Assert.Empty(log.RecentEntries);
    }

    [Fact]
    public async Task ObservableObjectAndAsyncCommandNotifyWithoutOverlappingAndReportErrors()
    {
        var probe = new ObservableProbe();
        string? changed = null;
        probe.PropertyChanged += (_, args) => changed = args.PropertyName;
        probe.Value = 3;
        Assert.Equal(nameof(ObservableProbe.Value), changed);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int executions = 0;
        Exception? reported = null;
        ICommand command = new AsyncCommand(async token =>
        {
            Interlocked.Increment(ref executions);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            throw new InvalidOperationException("expected");
        }, exception => reported = exception);

        command.Execute(null);
        await entered.Task;
        Assert.False(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(1, executions);
        release.SetResult();
        await Task.Run(async () =>
        {
            while (reported is null)
                await Task.Yield();
        }).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<InvalidOperationException>(reported);
        Assert.True(command.CanExecute(null));
    }

    private sealed class ObservableProbe : ObservableObject
    {
        private int _value;
        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}
