using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage]
public class ConfirmEmailRequest
{
    public string Token { get; set; } = string.Empty;
}
