namespace ThePredictions.Application.Repositories;

/// <summary>
/// What to store for one player in one league for one round.
/// </summary>
/// <remarks>
/// <see cref="BoostedPoints"/>, <see cref="HasBoost"/> and <see cref="AppliedBoostCode"/> are set explicitly rather than
/// left to the write to reset, because clearing a boost when base points are rebuilt is a rule: boosts are applied in the
/// step afterwards, so a stale boost left on the row would be counted twice.
/// </remarks>
public sealed record LeagueRoundScore(
    int LeagueId,
    string UserId,
    int BasePoints,
    int BoostedPoints,
    bool HasBoost,
    string? AppliedBoostCode);
