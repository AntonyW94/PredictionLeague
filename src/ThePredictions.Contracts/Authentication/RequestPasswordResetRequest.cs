using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class RequestPasswordResetRequest
{
    public string Email { get; init; } = string.Empty;
}
