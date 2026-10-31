namespace Growcery.Api.Infrastructure;

// Which Auth0 tenant and API the access tokens must come from (spec 003 §2.3). None of these values are secrets.
public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    // Added to access tokens by an Auth0 Post-Login Action (spec 003 D22). Auth0 requires custom claims to be namespaced.
    public const string NameClaim = "https://growcery.app/name";

    public string Domain { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;
}
