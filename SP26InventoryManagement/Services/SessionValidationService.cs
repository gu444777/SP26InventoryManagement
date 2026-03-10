using SP26InventoryManagement.DTOs;
using SP26InventoryManagement.Infrastructure;
using SP26InventoryManagement.Repositories;

namespace SP26InventoryManagement.Services;

public class SessionValidationService : ISessionValidationService
{
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

    public async Task<OperationResult> EnsureSessionForUserAsync(int expectedUserId, string? requiredRoleCode, CancellationToken ct)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return OperationResult.Failure("Session expired. Please log in again.");
        }

        if (_currentUserContext.UserId.Value != expectedUserId)
        {
            return OperationResult.Failure("Session mismatch detected. Please log in again.");
        }

        if (!_currentUserContext.TryTouchSession())
        {
            return OperationResult.Failure("Session expired. Please log in again.");
        }

        bool needRevalidation = !string.IsNullOrWhiteSpace(requiredRoleCode)
            || _currentUserContext.NeedsRevalidation(requiredRoleCode);
        if (needRevalidation)
        {
            var user = await _userRepository.GetByIdWithRolesAsync(expectedUserId, ct);
            if (user is null || !user.IsActive)
            {
                _currentUserContext.Clear();
                return OperationResult.Failure("Your account is inactive or unavailable. Please contact administrator.");
            }

            IReadOnlyCollection<string> roleCodes = user.UserRoleUsers
                .Where(userRole => userRole.Role.IsActive)
                .Select(userRole => userRole.Role.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _currentUserContext.SetUser(user.UserId, user.Username, user.FullName, roleCodes);
        }

        if (!string.IsNullOrWhiteSpace(requiredRoleCode) && !_currentUserContext.IsInRole(requiredRoleCode))
        {
            return OperationResult.Failure($"Access denied. Role '{requiredRoleCode}' is required.");
        }

        return OperationResult.Success();
    }
}
