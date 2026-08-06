using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

[ExcludeFromCodeCoverage]
public record ChaseUserDto(string Email, string FirstName, string RoundName, DateTime DeadlineUtc, string UserId);
