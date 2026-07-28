using MediatR;
using ThePredictions.Application.Features.Badges;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Sends the round-results digest email to every user who predicted in the round.
/// Fired automatically when a round completes; idempotent via Round.ResultsDigestSentUtc.
/// Set <see cref="Force"/> to re-send (admin "resend digest" action).
/// <see cref="BadgesAwarded"/> carries the badges genuinely earned this round (from the badge
/// evaluation that runs just before the digest) so the email can celebrate them; an admin resend
/// passes none, so the badges section is simply omitted.
/// </summary>
public record SendRoundDigestEmailsCommand(
    int RoundId,
    bool Force = false,
    IReadOnlyList<RoundBadgeAward>? BadgesAwarded = null) : IRequest;
