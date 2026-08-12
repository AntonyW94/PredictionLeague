using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Homepage.Queries;

/// <summary>One approved membership of a league in a season.</summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record HomepageMembershipRow(int SeasonId, string UserId);
