using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>One league and its season, as a prize evaluation judges them.</summary>
/// <remarks>
/// Both of the administrator's name parts arrive raw - abbreviating them is a rule, and this was one of the last two places the
/// abbreviation was still written out in SQL. <see cref="EntryDeadlineUtc"/> is nullable because the column is, which the row it
/// replaces denied.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PrizeLeagueRow(
    int LeagueId,
    string LeagueName,
    string AdministratorUserId,
    string? AdministratorFirstName,
    string? AdministratorLastName,
    string? EntryCode,
    decimal EntryCost,
    decimal? PrizeFundOverride,
    DateTime? EntryDeadlineUtc,
    string SeasonName,
    DateTime SeasonStartDateUtc,
    DateTime SeasonEndDateUtc,
    int NumberOfRounds,
    int EntrantCount);
