namespace SP26InventoryManagement.Services;

public interface IAuditLogService
{
    Task LogAsync(
        string actionType,
        string entityName,
        string? entityId,
        int? userId,
        bool isSuccess,
        string severity,
        object? details,
        string? clientIp,
        string? clientApp,
        CancellationToken ct);
}
