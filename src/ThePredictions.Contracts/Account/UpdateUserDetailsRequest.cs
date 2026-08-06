using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Account;

[ExcludeFromCodeCoverage]
public class UpdateUserDetailsRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public bool MarketingOptIn { get; init; }
}
