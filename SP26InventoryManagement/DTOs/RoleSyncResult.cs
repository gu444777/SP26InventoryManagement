namespace SP26InventoryManagement.DTOs;

public class RoleSyncResult
{
    public IReadOnlyCollection<int> AddedRoleIds { get; init; } = Array.Empty<int>();

    public IReadOnlyCollection<int> RemovedRoleIds { get; init; } = Array.Empty<int>();
}
