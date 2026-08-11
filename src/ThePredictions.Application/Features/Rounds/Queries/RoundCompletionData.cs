using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// Everything the round-completion view needs, uninterpreted: the round with its fixtures, the team names to
/// label them, the players who could be predicting them, and the predictions that exist.
///
/// Nothing is filtered by predictability and nothing is counted. Which fixtures are still open, who is missing
/// which, and how the round and its players are named are all decided in C#, so the same facts can serve the
/// admin view and the reminder job without either restating a rule.
/// </summary>
/// <remarks>
/// <see cref="Round"/> is the domain entity rather than flat columns, deliberately. Whether a fixture can still
/// be predicted is <c>Match.IsOpenForPrediction</c> and the round's display name is
/// <c>Round.GetDisplayNameOrDefault</c>; flat rows would need both restated against their fields, which is a
/// fresh copy of the very rules this work exists to collapse. The first draft of this port did exactly that and
/// duplicated the naming rule within minutes of it being written.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record RoundCompletionData(
    Round Round,
    IReadOnlyDictionary<int, RoundFixtureTeams> TeamNames,
    IReadOnlyList<RoundParticipantRow> Participants,
    IReadOnlyList<RoundPredictionRow> Predictions);
