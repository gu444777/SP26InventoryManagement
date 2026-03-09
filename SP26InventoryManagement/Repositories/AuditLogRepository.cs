using SP26InventoryManagement.Models;

namespace SP26InventoryManagement.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly Sp26inventoryManagementDbContext _dbContext;

    public AuditLogRepository(Sp26inventoryManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLog logEntry, CancellationToken ct)
    {
        _dbContext.AuditLogs.Add(logEntry);
        await _dbContext.SaveChangesAsync(ct);
    }
}
