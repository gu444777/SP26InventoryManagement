using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Repositories;
using SP26InventoryManagement.Services;

namespace SP26InventoryManagement.ViewModels;

public class CreateUserViewModel : ObservableObject
{
    private readonly IUserManagementService _userManagementService;
    private readonly IRoleRepository _roleRepository;
    private readonly CurrentUserContext _currentUserContext;
    private readonly IMessageService _messageService;
    private readonly AsyncRelayCommand _createUserCommand;
    private readonly RelayCommand _cancelCommand;

    private string _username = string.Empty;
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _phoneNumber = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public CreateUserViewModel(
        IUserManagementService userManagementService,
        IRoleRepository roleRepository,
        CurrentUserContext currentUserContext,
        IMessageService messageService)
    {
        _userManagementService = userManagementService;
        _roleRepository = roleRepository;
        _currentUserContext = currentUserContext;
        _messageService = messageService;

        RoleSelections = [];
        _createUserCommand = new AsyncRelayCommand(CreateUserAsync, CanCreateUser);
        _cancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }

    public event Action<bool>? CloseRequested;

    public ObservableCollection<RoleSelectionItem> RoleSelections { get; }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                _createUserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string FullName
    {
        get => _fullName;
        set
        {
            if (SetProperty(ref _fullName, value))
            {
                _createUserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string PhoneNumber
    {
        get => _phoneNumber;
        set => SetProperty(ref _phoneNumber, value);
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
                _createUserCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand CreateUserCommand => _createUserCommand;

    public ICommand CancelCommand => _cancelCommand;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ErrorMessage = string.Empty;
        RoleSelections.Clear();

        IReadOnlyList<RoleOptionDto> roles = await _roleRepository.GetActiveRolesAsync(ct);
        foreach (RoleOptionDto role in roles)
        {
            RoleSelectionItem roleSelection = new()
            {
                RoleId = role.RoleId,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName
            };

            roleSelection.PropertyChanged += OnRoleSelectionChanged;
            RoleSelections.Add(roleSelection);
        }
    }

    private bool CanCreateUser()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(FullName)
            && RoleSelections.Any(role => role.IsSelected);
    }

    private async Task CreateUserAsync()
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            ErrorMessage = "Your session has expired.";
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            CreateUserRequest request = new()
            {
                Username = Username.Trim(),
                FullName = FullName.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
                RoleIds = RoleSelections.Where(role => role.IsSelected).Select(role => role.RoleId).ToArray()
            };

            CreateUserResult result = await _userManagementService.CreateUserAsync(
                request,
                actorUserId: _currentUserContext.UserId.Value,
                ct: CancellationToken.None);

            if (!result.IsSuccess)
            {
                ErrorMessage = result.ErrorMessage ?? "Unable to create user.";
                return;
            }

            _messageService.ShowInfo(
                $"User created successfully.\n\nUsername: {request.Username}\nTemporary password: {result.GeneratedPassword}",
                "User Created");

            CloseRequested?.Invoke(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnRoleSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoleSelectionItem.IsSelected))
        {
            _createUserCommand.RaiseCanExecuteChanged();
        }
    }
}
