using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class ConfirmEmailRequest
{
    public string Token { get; set; } = string.Empty;
}
