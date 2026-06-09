namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// One league's slice of a user's round digest. <see cref="PositionDelta"/> is places gained:
/// positive means the user moved up the table this round, negative means they dropped, 0 or null
/// means no change / not available.
/// </summary>
public record LeagueRoundDigest(
    string LeagueName,
    int Points,
    int? Position,
    int? PositionDelta,
    string? TopScorerName,
    int? TopScorerPoints);
