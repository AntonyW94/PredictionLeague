using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>
/// An approved member of the league. Carries the raw name parts rather than a formatted display name:
/// formatting is <see cref="Domain.Services.PlayerDisplayName"/>'s job, not the database's.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record BoostMemberRow(
    string UserId,
    string FirstName,
    string LastName);
