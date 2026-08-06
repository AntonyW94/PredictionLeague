using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Authentication;

[ExcludeFromCodeCoverage]
public class RefreshTokenRequest
{
    public string? Token { get; init; }
}
