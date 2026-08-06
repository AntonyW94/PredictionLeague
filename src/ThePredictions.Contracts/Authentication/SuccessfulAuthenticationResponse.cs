using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SuccessfulAuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshTokenForCookie
) : AuthenticationResponse(true);
