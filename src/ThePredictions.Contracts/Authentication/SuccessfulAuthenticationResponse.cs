using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage]
public record SuccessfulAuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshTokenForCookie
) : AuthenticationResponse(true);
