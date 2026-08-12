namespace ThePredictions.Persistence.Conformance;

/// <summary>One player's stored outcome tally for a round, as the database holds it.</summary>
public sealed record StoredRoundResult(int ExactScoreCount, int CorrectResultCount, int IncorrectCount);
