namespace SP26InventoryManagement.DTOs;

public class UserListItemDto
{
    public int UserId { get; init; }

    public string Username { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }

    public DateTime? LastLoginAt { get; init; }

    public byte[] RowVersion { get; init; } = Array.Empty<byte>();

    public IReadOnlyList<RoleOptionDto> Roles { get; init; } = Array.Empty<RoleOptionDto>();

    public string RolesDisplay => string.Join(", ", Roles.Select(role => role.RoleCode));
}
