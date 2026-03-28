namespace SP26InventoryManagement.DTOs;

public class CreateUserRequest
{
    public string Username { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public IReadOnlyCollection<int> RoleIds { get; init; } = Array.Empty<int>();

    public int? WarehouseId { get; init; }
}
