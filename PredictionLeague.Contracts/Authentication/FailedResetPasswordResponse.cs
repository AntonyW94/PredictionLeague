namespace PredictionLeague.Contracts.Authentication;

public record FailedResetPasswordResponse(string Message) : ResetPasswordResponse(false);
