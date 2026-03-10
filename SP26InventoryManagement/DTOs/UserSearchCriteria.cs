namespace SP26InventoryManagement.DTOs;

public class UserSearchCriteria
{
    public string? SearchText { get; init; }

    public int? RoleId { get; init; }

    public bool? IsActive { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
