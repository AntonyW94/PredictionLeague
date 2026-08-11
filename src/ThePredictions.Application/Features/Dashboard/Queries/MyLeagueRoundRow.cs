using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// One round of a season the player has a league in, with everything the active-round rule needs to judge it.
/// </summary>
/// <remarks>
/// Drafts are included. Whether a draft round can be the one the tile shows is a rule - it cannot - and the old
/// statement expressed it as <c>r.[Status] &lt;&gt; @DraftStatus</c> in a <c>WHERE</c> clause, which put it out of
/// reach of a test.
///
/// <see cref="CompletedDateUtc"/> is what the forty-eight hour grace period turns on, and it is separate from the
/// status: a round marked complete with no completion date recorded has not completed for that purpose.
///
/// <see cref="Stages"/> is the raw tournament stage text, or null when the round has no mapping at all. The
/// distinction matters: an unmapped round shows no stage on the tile, whereas a mapped round whose text does not
/// mention a group stage is a knockout round.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record MyLeagueRoundRow(
    int RoundId,
    int SeasonId,
    int RoundNumber,
    string DisplayName,
    DateTime StartDateUtc,
    DateTime? CompletedDateUtc,
    RoundStatus Status,
    int InProgressMatchCount,
    int CompletedMatchCount,
    string? Stages);
