using System.Windows.Input;

namespace SP26InventoryManagement.ViewModels;

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task>? _executeAsync;
    private readonly Func<object?, Task>? _executeWithParameterAsync;
    private readonly Func<bool>? _canExecute;
    private readonly Func<object?, bool>? _canExecuteWithParameter;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync;
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null)
    {
        _executeWithParameterAsync = executeAsync;
        _canExecuteWithParameter = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        if (_executeWithParameterAsync is not null)
        {
            return !_isRunning && (_canExecuteWithParameter?.Invoke(parameter) ?? true);
        }

        return !_isRunning && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            if (_executeWithParameterAsync is not null)
            {
                await _executeWithParameterAsync(parameter);
            }
            else if (_executeAsync is not null)
            {
                await _executeAsync();
            }
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
