using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage]
public record FailedAuthenticationResponse(string Message) : AuthenticationResponse(false);
