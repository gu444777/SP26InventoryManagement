using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly AsyncRelayCommand _loginCommand;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        _loginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
    }

    public event Action<LoginResult>? LoginSucceeded;

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                _loginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                _loginCommand.RaiseCanExecuteChanged();
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
                _loginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand LoginCommand => _loginCommand;

    private bool CanLogin()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }

    private async Task LoginAsync()
    {
        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            LoginResult result = await _authService.LoginAsync(
                Username.Trim(),
                Password,
                clientIp: null,
                clientApp: "WPF-Client",
                ct: CancellationToken.None);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Login failed.";
                return;
            }

            LoginSucceeded?.Invoke(result);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
