namespace SP26InventoryManagement.DTOs;

public class ResetPasswordResult : OperationResult
{
    public string? GeneratedPassword { get; init; }

    public static ResetPasswordResult Success(string generatedPassword)
    {
        return new ResetPasswordResult
        {
            IsSuccess = true,
            GeneratedPassword = generatedPassword
        };
    }

    public new static ResetPasswordResult Failure(string errorMessage)
    {
        return new ResetPasswordResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
