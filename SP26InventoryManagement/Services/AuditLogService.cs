using System.Text.Json;
using SP26InventoryManagement.Models;
using SP26InventoryManagement.Repositories;

namespace SP26InventoryManagement.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;

    public AuditLogService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public Task LogAsync(
        string actionType,
        string entityName,
        string? entityId,
        int? userId,
        bool isSuccess,
        string severity,
        object? details,
        string? clientIp,
        string? clientApp,
        CancellationToken ct)
    {
        AuditLog log = new()
        {
            OccurredAt = DateTime.UtcNow,
            UserId = userId,
            ActionType = actionType,
            EntityName = entityName,
            EntityId = entityId,
            IsSuccess = isSuccess,
            Severity = string.IsNullOrWhiteSpace(severity) ? "INFO" : severity.Trim().ToUpperInvariant(),
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
            ClientIp = clientIp,
            ClientApp = clientApp
        };

        return _auditLogRepository.AddAsync(log, ct);
    }
}
