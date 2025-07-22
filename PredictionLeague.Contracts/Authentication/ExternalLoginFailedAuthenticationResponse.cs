namespace PredictionLeague.Contracts.Authentication;

public record ExternalLoginFailedAuthenticationResponse(
    string Message,
    string Source
) : FailedAuthenticationResponse(Message);