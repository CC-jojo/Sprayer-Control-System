using System.Windows.Input;

namespace SprayerControl.App.ViewModels;

public sealed class AsyncCommand : ObservableObject, ICommand
{
    private readonly Func<CancellationToken, Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _reportException;
    private int _isExecuting;

    public AsyncCommand(
        Func<CancellationToken, Task> executeAsync,
        Action<Exception>? reportException = null,
        Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _reportException = reportException;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting => Volatile.Read(ref _isExecuting) != 0;

    public bool CanExecute(object? parameter) => !IsExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            return;

        OnPropertyChanged(nameof(IsExecuting));
        RaiseCanExecuteChanged();
        try
        {
            await _executeAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _reportException?.Invoke(exception);
        }
        finally
        {
            Volatile.Write(ref _isExecuting, 0);
            OnPropertyChanged(nameof(IsExecuting));
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
