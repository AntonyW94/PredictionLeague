using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Rounds;

/// <summary>
/// Prediction-completion overview for a round. Powers both the admin round view (all participants
/// across the season) and the league-dashboard tile (that league's members). "Predictable" fixtures
/// are those with confirmed teams that are not postponed and have not yet locked - i.e. matches a
/// player can still act on.
/// </summary>
[ExcludeFromCodeCoverage]
public record RoundCompletionDto(
    int RoundId,
    string RoundName,
    DateTime DeadlineUtc,
    bool DeadlinePassed,
    bool CanSendReminders,
    int PredictableMatchCount,
    IReadOnlyList<RoundCompletionPlayerDto> Players);
