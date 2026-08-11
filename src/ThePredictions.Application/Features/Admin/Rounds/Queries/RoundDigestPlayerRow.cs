using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One player who was scored for this round, and how they did in it.</summary>
/// <remarks>
/// <see cref="PredictionCount"/> is how many of the round's fixtures they predicted. Whether that makes them somebody
/// to email is a rule - the old statement asked it as an <c>EXISTS</c> in its <c>WHERE</c> clause.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundDigestPlayerRow(
    string UserId,
    string Email,
    string FirstName,
    int ExactScoreCount,
    int CorrectResultCount,
    int PredictionCount);
