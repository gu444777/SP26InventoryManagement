using System.Windows.Input;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly CurrentUserContext _currentUserContext;
    private readonly IUserDialogService _userDialogService;
    private readonly AsyncRelayCommand _openChangePasswordCommand;

    public MainWindowViewModel(CurrentUserContext currentUserContext, IUserDialogService userDialogService)
    {
        _currentUserContext = currentUserContext;
        _userDialogService = userDialogService;
        _openChangePasswordCommand = new AsyncRelayCommand(OpenChangePasswordAsync, CanOpenChangePassword);
    }

    public string Username => _currentUserContext.Username;

    public string FullName => _currentUserContext.FullName;

    public ICommand OpenChangePasswordCommand => _openChangePasswordCommand;

    private bool CanOpenChangePassword()
    {
        return _currentUserContext.UserId.HasValue;
    }

    private Task OpenChangePasswordAsync()
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return Task.CompletedTask;
        }

        return _userDialogService.ShowChangePasswordDialogAsync(
            _currentUserContext.UserId.Value,
            _currentUserContext.Username,
            CancellationToken.None);
    }
}
