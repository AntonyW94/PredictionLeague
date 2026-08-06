using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record ChaseUserDto(string Email, string FirstName, string RoundName, DateTime DeadlineUtc, string UserId);
