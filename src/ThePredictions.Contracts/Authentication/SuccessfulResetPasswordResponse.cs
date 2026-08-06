using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record SuccessfulResetPasswordResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshTokenForCookie
) : ResetPasswordResponse(true);
