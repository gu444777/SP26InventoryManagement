namespace SP26InventoryManagement.Infrastructure;

public class CurrentUserContext
{
    public int? UserId { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> RoleCodes { get; private set; } = Array.Empty<string>();

    public bool IsAuthenticated => UserId.HasValue;

    public void SetUser(int userId, string username, string fullName, IReadOnlyCollection<string> roleCodes)
    {
        UserId = userId;
        Username = username;
        FullName = fullName;
        RoleCodes = roleCodes;
    }

    public void Clear()
    {
        UserId = null;
        Username = string.Empty;
        FullName = string.Empty;
        RoleCodes = Array.Empty<string>();
    }

    public bool IsInRole(string roleCode)
    {
        return RoleCodes.Any(code => string.Equals(code, roleCode, StringComparison.OrdinalIgnoreCase));
    }
}
