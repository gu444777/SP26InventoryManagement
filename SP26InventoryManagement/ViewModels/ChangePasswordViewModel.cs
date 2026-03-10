using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class ChangePasswordViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IMessageService _messageService;
    private readonly AsyncRelayCommand _changePasswordCommand;
    private readonly RelayCommand _cancelCommand;

    private int _userId;
    private string _username = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public ChangePasswordViewModel(IAuthService authService, IMessageService messageService)
    {
        _authService = authService;
        _messageService = messageService;
        _changePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync, CanChangePassword);
        _cancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    public int UserId
    {
        get => _userId;
        private set
        {
            if (SetProperty(ref _userId, value))
            {
                _changePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Username
    {
        get => _username;
        private set => SetProperty(ref _username, value);
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set
        {
            if (SetProperty(ref _currentPassword, value))
            {
                _changePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (SetProperty(ref _newPassword, value))
            {
                _changePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                _changePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _changePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand ChangePasswordCommand => _changePasswordCommand;

    public ICommand CancelCommand => _cancelCommand;

    public void Initialize(int userId, string username)
    {
        UserId = userId;
        Username = username;
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = string.Empty;
    }

    private bool CanChangePassword()
    {
        return !IsBusy
            && UserId > 0
            && !string.IsNullOrWhiteSpace(CurrentPassword)
            && !string.IsNullOrWhiteSpace(NewPassword)
            && !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    private async Task ChangePasswordAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            OperationResult result = await _authService.ChangePasswordAsync(
                UserId,
                CurrentPassword,
                NewPassword,
                ConfirmPassword,
                CancellationToken.None);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to change password.";
                return;
            }

            _messageService.ShowInfo("Password changed successfully.");
            CloseRequested?.Invoke(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
