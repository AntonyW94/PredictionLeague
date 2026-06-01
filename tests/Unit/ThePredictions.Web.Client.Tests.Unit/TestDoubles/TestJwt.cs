using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ThePredictions.Web.Client.Tests.Unit.TestDoubles;

/// <summary>Builds JWTs with a chosen expiry for tests (the client only reads them, never validates the signature).</summary>
public static class TestJwt
{
    private static readonly SigningCredentials SigningCredentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-that-is-at-least-32-bytes")),
        SecurityAlgorithms.HmacSha256);

    public static string WithExpiry(DateTime expiresUtc, params Claim[] claims)
    {
        var token = new JwtSecurityToken(
            claims: claims,
            expires: expiresUtc,
            signingCredentials: SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>A token comfortably inside its validity window (well beyond the 30s refresh leeway).</summary>
    public static string Valid(params Claim[] claims) => WithExpiry(DateTime.UtcNow.AddMinutes(15), claims);

    /// <summary>An already-expired token, forcing a refresh.</summary>
    public static string Expired() => WithExpiry(DateTime.UtcNow.AddMinutes(-1));
}
