using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage]
public record ExternalLoginFailedAuthenticationResponse(
    string Message,
    string Source
) : FailedAuthenticationResponse(Message);
