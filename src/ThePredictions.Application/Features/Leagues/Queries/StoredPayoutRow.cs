using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// What the league has recorded as paid to one winner.
/// </summary>
/// <remarks>
/// <see cref="TotalAmount"/> is what was owed when the payment was recorded, which is not necessarily what is owed now:
/// comparing the two is how the screen spots that prizes changed after somebody was paid.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record StoredPayoutRow(string UserId, decimal TotalAmount, DateTime? PaidAtUtc);
