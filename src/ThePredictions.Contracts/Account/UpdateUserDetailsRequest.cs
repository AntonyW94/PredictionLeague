using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Account;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public class UpdateUserDetailsRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public bool MarketingOptIn { get; init; }
}
