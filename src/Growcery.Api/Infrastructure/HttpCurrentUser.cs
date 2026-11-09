using System.Security.Claims;
using Growcery.Core.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Growcery.Api.Infrastructure;

// Tells the business layer who is signed in, from the validated access token, without it knowing about HTTP or Auth0.
public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal User =>
        _httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("There is no current HTTP request.");

    // The Auth0 user ID, e.g. "auth0|6721f0c2…" (spec 003 D21).
    public string ExternalId =>
        User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException("The current user is not signed in.");

    // The namespaced name claim (spec 003 D22) when Auth0 sends it, otherwise the user ID.
    public string DisplayName =>
        User.Identity?.Name is { } name && !string.IsNullOrWhiteSpace(name) ? name : ExternalId;
}
