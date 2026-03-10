namespace SP26InventoryManagement.Infrastructure;

public class CurrentUserContext
{
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RevalidationInterval = TimeSpan.FromMinutes(5);

    public int? UserId { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> RoleCodes { get; private set; } = Array.Empty<string>();

    public DateTime? SessionExpiresAtUtc { get; private set; }

    public DateTime? LastRevalidatedAtUtc { get; private set; }

    public bool IsAuthenticated => UserId.HasValue && SessionExpiresAtUtc.HasValue && SessionExpiresAtUtc.Value > DateTime.UtcNow;

    public void SetUser(int userId, string username, string fullName, IReadOnlyCollection<string> roleCodes)
    {
        DateTime now = DateTime.UtcNow;
        UserId = userId;
        Username = username;
        FullName = fullName;
        RoleCodes = roleCodes;
        LastRevalidatedAtUtc = now;
        SessionExpiresAtUtc = now.Add(SessionTimeout);
    }

    public void Clear()
    {
        UserId = null;
        Username = string.Empty;
        FullName = string.Empty;
        RoleCodes = Array.Empty<string>();
        SessionExpiresAtUtc = null;
        LastRevalidatedAtUtc = null;
    }

    public bool IsInRole(string roleCode)
    {
        return RoleCodes.Any(code => string.Equals(code, roleCode, StringComparison.OrdinalIgnoreCase));
    }

    public bool TryTouchSession()
    {
        if (!UserId.HasValue)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        if (!SessionExpiresAtUtc.HasValue || SessionExpiresAtUtc.Value <= now)
        {
            Clear();
            return false;
        }

        SessionExpiresAtUtc = now.Add(SessionTimeout);
        return true;
    }

    public bool NeedsRevalidation(string? requiredRoleCode = null)
    {
        if (!UserId.HasValue)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(requiredRoleCode) && !IsInRole(requiredRoleCode))
        {
            return true;
        }

        if (!LastRevalidatedAtUtc.HasValue)
        {
            return true;
        }

        return DateTime.UtcNow - LastRevalidatedAtUtc.Value >= RevalidationInterval;
    }
}
