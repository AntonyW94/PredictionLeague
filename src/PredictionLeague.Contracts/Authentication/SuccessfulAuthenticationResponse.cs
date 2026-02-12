namespace PredictionLeague.Contracts.Authentication;

public record SuccessfulAuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshTokenForCookie
) : AuthenticationResponse(true);