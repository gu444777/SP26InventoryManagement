namespace SP26InventoryManagement.DTOs;

public class CreateUserResult : OperationResult
{
    public int? UserId { get; init; }

    public string? GeneratedPassword { get; init; }

    public static CreateUserResult Success(int userId, string generatedPassword)
    {
        return new CreateUserResult
        {
            IsSuccess = true,
            UserId = userId,
            GeneratedPassword = generatedPassword
        };
    }

    public new static CreateUserResult Failure(string errorMessage)
    {
        return new CreateUserResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
