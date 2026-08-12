namespace ThePredictions.Domain.Services;

/// <summary>How one player's predictions in one round turned out.</summary>
/// <remarks>
/// Predictions still waiting on a result are in none of the three. That is what makes the counts a summary of what has
/// been judged rather than of what was entered, and it is why they can all be nought for a player who predicted.
/// </remarks>
public sealed record OutcomeCounts(int ExactScoreCount, int CorrectResultCount, int IncorrectCount);
