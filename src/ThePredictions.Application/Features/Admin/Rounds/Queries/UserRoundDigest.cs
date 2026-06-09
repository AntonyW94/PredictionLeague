namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// One user's complete round-results digest, ready to render into an email: the round-level
/// summary plus a row per league they belong to in that round's season.
/// </summary>
public record UserRoundDigest(
    string UserId,
    string Email,
    string FirstName,
    string RoundName,
    int ExactScoreCount,
    int CorrectResultCount,
    string? NextRoundName,
    DateTime? NextRoundStartUtc,
    DateTime? NextRoundDeadlineUtc,
    IReadOnlyList<LeagueRoundDigest> Leagues);
