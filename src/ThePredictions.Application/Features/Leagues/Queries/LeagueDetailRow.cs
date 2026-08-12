using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One league's settings, as stored.
/// </summary>
/// <remarks>
/// <see cref="EntryCode"/> and <see cref="EntryDeadlineUtc"/> are nullable here because that is what the columns are.
/// The old statement hid both behind <c>ISNULL</c>: a missing code became the word <c>'Public'</c> and a missing
/// deadline became <c>'1900-01-01'</c>. Those are presentation decisions and they belong in C#, where the second one in
/// particular can be seen for what it is.
///
/// Two member counts, and the handler uses <see cref="ApprovedMemberCount"/> - the same population every other member
/// count on the site uses. It used to use <see cref="TotalMembershipCount"/>, so a league with five members and two
/// outstanding requests reported seven. The total is still returned because the league prizes page counts that way, and
/// having both here is what made the difference visible in the first place.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueDetailRow(
    int Id,
    string Name,
    string SeasonName,
    int SeasonId,
    int TotalMembershipCount,
    int ApprovedMemberCount,
    decimal Price,
    string? EntryCode,
    DateTime? EntryDeadlineUtc,
    int PointsForExactScore,
    int PointsForCorrectResult,
    CompetitionType CompetitionType,
    bool HasPrizeScheme,
    bool RequiresMemberApproval,
    bool IsListed);
