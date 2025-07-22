namespace PredictionLeague.Contracts.Authentication;

public record SuccessfulAuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshTokenForCookie
) : AuthenticationResponse(true);