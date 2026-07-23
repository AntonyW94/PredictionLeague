using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Badges.Commands;

public class EvaluateBadgesForRoundCommandHandler(
    IRoundRepository roundRepository,
    IBadgeEvaluationRepository evaluationRepository,
    IUserBadgeRepository userBadgeRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<EvaluateBadgesForRoundCommand>
{
    private const int MinimumCrowdForBeatTheCrowd = 5;

    public async Task Handle(EvaluateBadgesForRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return;

        var seasonId = round.SeasonId;

        // The achievement date. Prefer the round's completion time; fall back to its last kick-off (the
        // round finished around then) when completion isn't recorded, so retrospective awards are dated
        // to when they really happened rather than to "now". Only an empty round uses the clock.
        var awardedUtc = round.CompletedDateUtc
            ?? (round.Matches.Any() ? round.Matches.Max(m => m.MatchDateTimeUtc) : dateTimeProvider.UtcNow);

        async Task Award(string userId, string badgeKey, DateTime whenUtc, int? leagueId = null, int? roundId = null, int? seasonScopeId = null, string? detail = null)
        {
            await userBadgeRepository.AwardAsync(
                AwardedBadge.Create(userId, badgeKey, whenUtc, leagueId, roundId, seasonScopeId, detail),
                cancellationToken);
        }

        // 1. Per-user round results: first steps + Sharpshooter.
        var results = await evaluationRepository.GetRoundResultsAsync(round.Id, cancellationToken);
        foreach (var r in results)
        {
            await Award(r.UserId, BadgeKeys.OffTheMark, awardedUtc);

            if (r.TotalPoints > 0)
                await Award(r.UserId, BadgeKeys.OnTheBoard, awardedUtc);

            if (r.ExactScoreCount >= 1)
                await Award(r.UserId, BadgeKeys.FirstBlood, awardedUtc);

            var exactDetail = $"{r.ExactScoreCount} in a round";
            if (r.ExactScoreCount >= 5)
                await Award(r.UserId, BadgeKeys.Sharpshooter3, awardedUtc, roundId: round.Id, detail: exactDetail);
            if (r.ExactScoreCount >= 4)
                await Award(r.UserId, BadgeKeys.Sharpshooter2, awardedUtc, roundId: round.Id, detail: exactDetail);
            if (r.ExactScoreCount >= 3)
                await Award(r.UserId, BadgeKeys.Sharpshooter1, awardedUtc, roundId: round.Id, detail: exactDetail);
        }

        // 2. Marksman - cumulative exact scores in the season up to this round.
        var cumulative = await evaluationRepository.GetSeasonCumulativeExactsAsync(seasonId, round.RoundNumber, cancellationToken);
        foreach (var c in cumulative)
        {
            var detail = $"{c.Count} exact scores";
            if (c.Count >= 15)
                await Award(c.UserId, BadgeKeys.Marksman3, awardedUtc, seasonScopeId: seasonId, detail: detail);
            if (c.Count >= 10)
                await Award(c.UserId, BadgeKeys.Marksman2, awardedUtc, seasonScopeId: seasonId, detail: detail);
            if (c.Count >= 5)
                await Award(c.UserId, BadgeKeys.Marksman1, awardedUtc, seasonScopeId: seasonId, detail: detail);
        }

        // 3. On Fire - consecutive rounds with an exact score, ending at this round.
        var streaks = await evaluationRepository.GetStreaksEndingAtRoundAsync(seasonId, round.RoundNumber, cancellationToken);
        foreach (var s in streaks)
        {
            var detail = $"{s.Count} in a row";
            if (s.Count >= 7)
                await Award(s.UserId, BadgeKeys.OnFire3, awardedUtc, detail: detail);
            if (s.Count >= 5)
                await Award(s.UserId, BadgeKeys.OnFire2, awardedUtc, detail: detail);
            if (s.Count >= 3)
                await Award(s.UserId, BadgeKeys.OnFire1, awardedUtc, detail: detail);
        }

        // 4. Round Winner - first in a round in any league.
        var winners = await evaluationRepository.GetRoundWinnersAsync(round.Id, cancellationToken);
        foreach (var w in winners)
            await Award(w.UserId, BadgeKeys.RoundWinner, awardedUtc, leagueId: w.LeagueId, roundId: round.Id);

        // 5. Beat the Crowd - backed the minority result and won.
        var beatTheCrowd = await evaluationRepository.GetBeatTheCrowdUsersAsync(round.Id, MinimumCrowdForBeatTheCrowd, cancellationToken);
        foreach (var userId in beatTheCrowd)
            await Award(userId, BadgeKeys.BeatTheCrowd, awardedUtc, roundId: round.Id);

        // 6. Socialite - leagues joined all-time, dated to when each Nth league was joined.
        var socialiteAwards = await evaluationRepository.GetSocialiteAwardsAsync(cancellationToken);
        foreach (var s in socialiteAwards)
        {
            var badgeKey = s.Rank switch
            {
                1 => BadgeKeys.Socialite1,
                3 => BadgeKeys.Socialite2,
                5 => BadgeKeys.Socialite3,
                _ => null
            };

            if (badgeKey is not null)
                await Award(s.UserId, badgeKey, s.AwardedUtc, detail: $"{s.Rank} leagues");
        }

        // 7. Account/setup badges (add mobile, add bank details, create a league) for all users, dated to
        // when they did it. Evaluated here (idempotent) so they land at the next round completion.
        var accountAwards = await evaluationRepository.GetAccountBadgeAwardsAsync(cancellationToken);
        foreach (var a in accountAwards)
            await Award(a.UserId, a.BadgeKey, a.AwardedUtc);

        // 8. Season-end honours + Ever-Present.
        var isLastRound = await roundRepository.IsLastRoundOfSeasonAsync(round.Id, seasonId, cancellationToken);
        if (!isLastRound)
            return;

        var standings = await evaluationRepository.GetSeasonStandingsAsync(seasonId, cancellationToken);
        foreach (var s in standings)
        {
            if (s.Rank == 1)
                await Award(s.UserId, BadgeKeys.Champion, awardedUtc, leagueId: s.LeagueId);
            if (s.Rank <= 3)
                await Award(s.UserId, BadgeKeys.Podium, awardedUtc, leagueId: s.LeagueId);
        }

        var everPresent = await evaluationRepository.GetEverPresentUsersAsync(seasonId, cancellationToken);
        foreach (var userId in everPresent)
            await Award(userId, BadgeKeys.EverPresent, awardedUtc, seasonScopeId: seasonId);
    }
}
