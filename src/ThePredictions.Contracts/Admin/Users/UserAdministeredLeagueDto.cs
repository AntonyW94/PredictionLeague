using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Users;

/// <summary>One league an account runs.</summary>
/// <remarks>
/// <see cref="Price"/> is the league's entry fee, not money this account paid - running a league and playing in it are
/// separate things, which is what <see cref="AlsoPlaying"/> answers.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record UserAdministeredLeagueDto(
    int LeagueId,
    string LeagueName,
    int SeasonId,
    string SeasonName,
    bool IsCurrentSeason,
    bool IsFree,
    decimal Price,
    int ApprovedMemberCount,
    bool AlsoPlaying);
