namespace ThePredictions.Persistence.Conformance;

/// <summary>One player's stored points for one round of one league, as the database holds it.</summary>
public sealed record StoredLeagueRoundResult(
    int BasePoints,
    int BoostedPoints,
    bool HasBoost,
    string? AppliedBoostCode);
