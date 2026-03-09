namespace SP26InventoryManagement.DTOs;

public class RoleOptionDto
{
    public int RoleId { get; init; }

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"{RoleName} ({RoleCode})";
    }
}
