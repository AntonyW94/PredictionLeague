using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record ExternalLoginFailedAuthenticationResponse(
    string Message,
    string Source
) : FailedAuthenticationResponse(Message);
