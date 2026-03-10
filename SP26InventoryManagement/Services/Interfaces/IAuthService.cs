using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password, string? clientIp, string? clientApp, CancellationToken ct);

    Task<OperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword, CancellationToken ct);

    void Logout();
}
