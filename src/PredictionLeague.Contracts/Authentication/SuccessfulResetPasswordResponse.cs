namespace PredictionLeague.Contracts.Authentication;

public record SuccessfulResetPasswordResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshTokenForCookie
) : ResetPasswordResponse(true);
