namespace PredictionLeague.Contracts.Authentication;

public record FailedAuthenticationResponse(string Message) : AuthenticationResponse(false);