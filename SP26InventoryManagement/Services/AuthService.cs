using Microsoft.EntityFrameworkCore;
using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Repositories;

namespace SP26InventoryManagement.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;
    private readonly CurrentUserContext _currentUserContext;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService,
        CurrentUserContext currentUserContext)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _currentUserContext = currentUserContext;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, string? clientIp, string? clientApp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return LoginResult.Failure("Username and password are required.");
        }

        string normalizedUsername = username.Trim();
        var user = await _userRepository.GetByUsernameWithRolesAsync(normalizedUsername, ct);

        if (user is null || !user.IsActive)
        {
            await _auditLogService.LogAsync(
                actionType: "LOGIN_FAILED",
                entityName: "Users",
                entityId: normalizedUsername,
                userId: null,
                isSuccess: false,
                severity: "WARN",
                details: new { Username = normalizedUsername, Reason = "INVALID_CREDENTIALS_OR_INACTIVE" },
                clientIp: clientIp,
                clientApp: clientApp,
                ct: ct);

            return LoginResult.Failure("Invalid username or password.");
        }

        if (!_passwordHasher.Verify(password, user.PasswordHash))
        {
            await _auditLogService.LogAsync(
                actionType: "LOGIN_FAILED",
                entityName: "Users",
                entityId: user.UserId.ToString(),
                userId: user.UserId,
                isSuccess: false,
                severity: "WARN",
                details: new { Username = normalizedUsername, Reason = "INVALID_CREDENTIALS" },
                clientIp: clientIp,
                clientApp: clientApp,
                ct: ct);

            return LoginResult.Failure("Invalid username or password.");
        }

        IReadOnlyCollection<string> roleCodes = user.UserRoleUsers
            .Where(userRole => userRole.Role.IsActive)
            .Select(userRole => userRole.Role.RoleCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return LoginResult.Failure("Your account data changed while logging in. Please try again.");
        }
        catch (DbUpdateException)
        {
            return LoginResult.Failure("Login failed due to a database update issue.");
        }

        _currentUserContext.SetUser(user.UserId, user.Username, user.FullName, roleCodes);

        await _auditLogService.LogAsync(
            actionType: "LOGIN_SUCCESS",
            entityName: "Users",
            entityId: user.UserId.ToString(),
            userId: user.UserId,
            isSuccess: true,
            severity: "INFO",
            details: new { user.Username, Roles = roleCodes },
            clientIp: clientIp,
            clientApp: clientApp,
            ct: ct);

        return LoginResult.Success(user.UserId, user.Username, user.FullName, roleCodes);
    }

    public async Task<OperationResult> ChangePasswordAsync(int userId, string currentPassword, string newPassword, string confirmPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            return OperationResult.Failure("Please fill in all password fields.");
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            return OperationResult.Failure("New password and confirm password do not match.");
        }

        string passwordValidationError = ValidatePasswordPolicy(newPassword);
        if (!string.IsNullOrEmpty(passwordValidationError))
        {
            return OperationResult.Failure(passwordValidationError);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null || !user.IsActive)
        {
            return OperationResult.Failure("User account does not exist or is inactive.");
        }

        if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
        {
            await _auditLogService.LogAsync(
                actionType: "CHANGE_PASSWORD_FAILED",
                entityName: "Users",
                entityId: user.UserId.ToString(),
                userId: user.UserId,
                isSuccess: false,
                severity: "WARN",
                details: new { Reason = "INVALID_CURRENT_PASSWORD" },
                clientIp: null,
                clientApp: "WPF-Client",
                ct: ct);

            return OperationResult.Failure("Current password is incorrect.");
        }

        try
        {
            user.PasswordHash = _passwordHasher.Hash(newPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Failure("Password change conflict. Please reload and try again.");
        }
        catch (DbUpdateException)
        {
            return OperationResult.Failure("Password change failed due to a database update issue.");
        }

        await _auditLogService.LogAsync(
            actionType: "CHANGE_PASSWORD",
            entityName: "Users",
            entityId: user.UserId.ToString(),
            userId: user.UserId,
            isSuccess: true,
            severity: "INFO",
            details: new { Reason = "SELF_SERVICE" },
            clientIp: null,
            clientApp: "WPF-Client",
            ct: ct);

        return OperationResult.Success();
    }

    private static string ValidatePasswordPolicy(string password)
    {
        if (password.Length < 8)
        {
            return "Password must be at least 8 characters.";
        }

        if (!password.Any(char.IsUpper))
        {
            return "Password must contain at least one uppercase letter.";
        }

        if (!password.Any(char.IsLower))
        {
            return "Password must contain at least one lowercase letter.";
        }

        if (!password.Any(char.IsDigit))
        {
            return "Password must contain at least one digit.";
        }

        if (!password.Any(character => !char.IsLetterOrDigit(character)))
        {
            return "Password must contain at least one special character.";
        }

        return string.Empty;
    }
}
