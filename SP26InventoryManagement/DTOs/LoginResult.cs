namespace SP26InventoryManagement.DTOs;

public class LoginResult : OperationResult
{
    public int? UserId { get; init; }

    public string? Username { get; init; }

    public string? FullName { get; init; }

    public IReadOnlyCollection<string> RoleCodes { get; init; } = Array.Empty<string>();

    public static LoginResult Success(int userId, string username, string fullName, IReadOnlyCollection<string> roleCodes)
    {
        return new LoginResult
        {
            IsSuccess = true,
            UserId = userId,
            Username = username,
            FullName = fullName,
            RoleCodes = roleCodes
        };
    }

    public new static LoginResult Failure(string errorMessage)
    {
        return new LoginResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
