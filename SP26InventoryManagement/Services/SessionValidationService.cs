using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Repositories;

namespace SP26InventoryManagement.Services;

public class SessionValidationService : ISessionValidationService
{
    private const string SessionInvalidatedMessage = "Session invalidated because your permissions changed. Please log in again.";

    private readonly CurrentUserContext _currentUserContext;
    private readonly IUserRepository _userRepository;

    public SessionValidationService(CurrentUserContext currentUserContext, IUserRepository userRepository)
    {
        _currentUserContext = currentUserContext;
        _userRepository = userRepository;
    }

    public Task<OperationResult> EnsureCurrentSessionAsync(string? requiredRoleCode, CancellationToken ct)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return Task.FromResult(OperationResult.Failure("Session expired. Please log in again."));
        }

        return EnsureSessionForUserAsync(_currentUserContext.UserId.Value, requiredRoleCode, ct);
    }

    public async Task<OperationResult> EnsureSessionForUserAsync(
        int expectedUserId,
        string? requiredRoleCode,
        CancellationToken ct,
        bool forceRevalidation = false)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return OperationResult.Failure("Session expired. Please log in again.");
        }

        if (_currentUserContext.UserId.Value != expectedUserId)
        {
            _currentUserContext.Clear();
            return OperationResult.Failure("Session mismatch detected. Please log in again.");
        }

        if (!_currentUserContext.TryTouchSession())
        {
            return OperationResult.Failure("Session expired. Please log in again.");
        }

        var fingerprintUser = await _userRepository.GetByIdAsync(expectedUserId, ct);
        if (fingerprintUser is null || !fingerprintUser.IsActive || fingerprintUser.AuthVersion != _currentUserContext.AuthVersion)
        {
            _currentUserContext.Clear();
            return OperationResult.Failure(SessionInvalidatedMessage);
        }

        bool needRevalidation = forceRevalidation || _currentUserContext.NeedsRevalidation(requiredRoleCode);
        if (needRevalidation)
        {
            var user = await _userRepository.GetByIdWithRolesAsync(expectedUserId, ct);
            if (user is null || !user.IsActive || user.AuthVersion != fingerprintUser.AuthVersion)
            {
                _currentUserContext.Clear();
                return OperationResult.Failure(SessionInvalidatedMessage);
            }

            IReadOnlyCollection<string> roleCodes = user.UserRoleUsers
                .Where(userRole => userRole.Role.IsActive)
                .Select(userRole => userRole.Role.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _currentUserContext.SetUser(user.UserId, user.Username, user.FullName, roleCodes, user.AuthVersion);
        }

        if (!string.IsNullOrWhiteSpace(requiredRoleCode) && !_currentUserContext.IsInRole(requiredRoleCode))
        {
            return OperationResult.Failure($"Access denied. Role '{requiredRoleCode}' is required.");
        }

        return OperationResult.Success();
    }
}
