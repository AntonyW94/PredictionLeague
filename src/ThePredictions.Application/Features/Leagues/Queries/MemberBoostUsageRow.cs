using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// A boost one member played in one round of one league, with the artwork to show for it.
/// </summary>
/// <remarks>
/// Returned as rows rather than one per member. Nothing in the schema stops a member holding two usages for the
/// same round, and the old query relied on that being true in practice - it left-joined the usage into every
/// fixture row and then took the first non-empty code it saw. Which one wins is now a stated rule in the handler
/// rather than an accident of row order.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MemberBoostUsageRow(string UserId, string Code, string? ImageUrl);
