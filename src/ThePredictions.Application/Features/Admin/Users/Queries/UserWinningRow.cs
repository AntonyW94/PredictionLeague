using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

/// <summary>One prize an account has won.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record UserWinningRow(string UserId, decimal Amount);
