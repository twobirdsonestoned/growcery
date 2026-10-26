using System.Security.Claims;
using Growcery.Core.Abstractions;

namespace Growcery.Api.Infrastructure;

// Tells the business layer who is signed in, without it knowing about HTTP or ASP.NET Identity.
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User =>
        _httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("There is no current HTTP request.");

    public string ExternalId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("The current user is not signed in.");

    public string DisplayName => User.Identity?.Name ?? ExternalId;
}
