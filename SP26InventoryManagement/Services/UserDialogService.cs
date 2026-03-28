using System.Windows;
using Microsoft.Extensions.DependencyInjection;
namespace SP26InventoryManagement.Services;

public class UserDialogService : IUserDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public UserDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ShowCreateUserDialogAsync(CancellationToken ct)
    {
        var window = _serviceProvider.GetRequiredService<SP26InventoryManagement.CreateUserWindow>();
        window.Owner = GetCurrentOwner();
        await window.ViewModel.InitializeAsync(ct);

        bool? dialogResult = window.ShowDialog();
        return dialogResult == true;
    }

    public Task ShowStaffWarehouseAssignmentDialogAsync(CancellationToken ct)
    {
        _ = ct;
        var window = _serviceProvider.GetRequiredService<SP26InventoryManagement.StaffWarehouseAssignmentWindow>();
        window.Owner = GetCurrentOwner();
        window.ShowDialog();
        return Task.CompletedTask;
    }

    public Task ShowChangePasswordDialogAsync(int userId, string username, CancellationToken ct)
    {
        var window = _serviceProvider.GetRequiredService<SP26InventoryManagement.ChangePasswordWindow>();
        window.Owner = GetCurrentOwner();
        window.ViewModel.Initialize(userId, username);
        window.ShowDialog();
        return Task.CompletedTask;
    }

    private static Window? GetCurrentOwner()
    {
        return Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive) ?? Application.Current.MainWindow;
    }
}
