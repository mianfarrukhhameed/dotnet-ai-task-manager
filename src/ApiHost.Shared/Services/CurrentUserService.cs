using Fistix.TaskManager.Core.Abstractions.Repositories;
using Fistix.TaskManager.Core.Abstractions.Services;
using Fistix.TaskManager.Core.DomainModel.Aggregates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Fistix.TaskManager.ApiHost.Shared.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly ILogger<CurrentUserService> _logger;
    private UserProfile? _userProfile;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        IRepositoryFactory repositoryFactory,
        ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _repositoryFactory = repositoryFactory;
        _logger = logger;
    }

    public string Email => ResolveEmail(_httpContextAccessor.HttpContext?.User)!;

    public bool HasAdminProfile => UserProfile != null && UserProfile.ExternalId != Guid.Empty && UserProfile.IsAdmin;

    public UserProfile UserProfile
    {
        get
        {
            if (_userProfile == null && !string.IsNullOrWhiteSpace(Email))
            {
                _userProfile = _repositoryFactory.UserProfileRepository.GetByEmailAddress(Email).Result;

                if (_userProfile == null)
                {
                    _logger.LogWarning("No UserProfile found for email {Email}", Email);
                    _userProfile = new UserProfile();
                }
            }
            else if (_userProfile == null && _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning(
                    "Authenticated request has no email claim. Available claims: {Claims}",
                    string.Join(", ", _httpContextAccessor.HttpContext.User.Claims.Select(c => c.Type)));
            }

            return _userProfile!;
        }
    }

    private static string? ResolveEmail(ClaimsPrincipal? user)
    {
        if (user == null)
            return null;

        var email = user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        if (!string.IsNullOrWhiteSpace(email))
            return email.Trim();

        return user.Claims
            .FirstOrDefault(c => c.Type.EndsWith("/email", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
    }
}
