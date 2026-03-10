using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog logEntry, CancellationToken ct);
}
