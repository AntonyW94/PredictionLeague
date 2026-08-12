using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>One league as the management screen lists it.</summary>
/// <remarks>
/// <see cref="EntryCode"/> is null for a public league, which is the fact rather than the word "Public" the statement used to
/// substitute. What a null means to the screen, and who is allowed to see a code at all, are the handler's to decide.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record ManageLeagueRow(
    int Id,
    string Name,
    int SeasonId,
    string SeasonName,
    DateTime SeasonStartDateUtc,
    string AdministratorUserId,
    int MemberCount,
    decimal Price,
    string? EntryCode,
    DateTime? EntryDeadlineUtc,
    int PointsForExactScore,
    int PointsForCorrectResult);
