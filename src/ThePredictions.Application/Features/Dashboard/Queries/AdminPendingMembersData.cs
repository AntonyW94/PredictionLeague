using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// The leagues a player administers and the requests waiting on them.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdminPendingMembersData(
    IReadOnlyList<AdministeredLeagueRow> Leagues,
    IReadOnlyList<PendingMemberRow> PendingMembers);
