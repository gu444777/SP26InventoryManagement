using SP26InventoryManagement.DTOs;

namespace SP26InventoryManagement.Services;

public interface ISessionValidationService
{
    Task<OperationResult> EnsureCurrentSessionAsync(string? requiredRoleCode, CancellationToken ct);

    Task<OperationResult> EnsureSessionForUserAsync(int expectedUserId, string? requiredRoleCode, CancellationToken ct);
}
