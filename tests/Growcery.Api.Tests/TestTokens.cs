using System.Security.Cryptography;
using Growcery.Api.Infrastructure;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Growcery.Api.Tests;

// Mints access tokens shaped like Auth0's (RS256, sub, namespaced name claim), so tests never call Auth0.
public static class TestTokens
{
    public const string Issuer = "https://test.growcery.local/";
    public const string Audience = "https://api.growcery.test";

    public static readonly RsaSecurityKey SigningKey = CreateKey("test-key");

    public static RsaSecurityKey CreateKey(string keyId) => new(RSA.Create(2048)) { KeyId = keyId };

    public static string Create(
        string subject,
        string? name = null,
        DateTime? expires = null,
        string audience = Audience,
        string issuer = Issuer,
        SecurityKey? signingKey = null)
    {
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = subject };
        if (name is not null)
        {
            claims[Auth0Options.NameClaim] = name;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = expiresAt.AddHours(-1),
            NotBefore = expiresAt.AddHours(-1),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256),
        });
    }

    // An "alg": "none" token: valid claims, no signature.
    public static string CreateUnsigned(string subject)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = subject },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
        });
    }
}
