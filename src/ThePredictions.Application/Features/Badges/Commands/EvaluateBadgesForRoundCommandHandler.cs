using MediatR;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Badges.Commands;

public class EvaluateBadgesForRoundCommandHandler(
    IRoundRepository roundRepository,
    IBadgeEvaluationRepository evaluationRepository,
    IUserBadgeRepository userBadgeRepository,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<EvaluateBadgesForRoundCommand, IReadOnlyList<RoundBadgeAward>>
{
    private const int MinimumCrowdForBeatTheCrowd = 5;

    public async Task<IReadOnlyList<RoundBadgeAward>> Handle(EvaluateBadgesForRoundCommand request, CancellationToken cancellationToken)
    {
        var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);
        if (round is null)
            return [];

        var seasonId = round.SeasonId;

        // The achievement date. Prefer the round's completion time; fall back to its last kick-off (the
        // round finished around then) when completion isn't recorded, so retrospective awards are dated
        // to when they really happened rather than to "now". Only an empty round uses the clock.
        var awardedUtc = round.CompletedDateUtc
            ?? (round.Matches.Any() ? round.Matches.Max(m => m.MatchDateTimeUtc) : dateTimeProvider.UtcNow);

        var awarder = new BadgeAwarder(userBadgeRepository, cancellationToken);

        await AwardRoundResultBadgesAsync(awarder, round, awardedUtc, cancellationToken);
        await AwardSeasonProgressBadgesAsync(awarder, round, seasonId, awardedUtc, cancellationToken);
        await AwardRoundStandoutBadgesAsync(awarder, round, awardedUtc, cancellationToken);
        await AwardSocialiteBadgesAsync(awarder, cancellationToken);
        await AwardAccountBadgesAsync(awarder, cancellationToken);
        await AwardPeriodWinnerBadgesAsync(awarder, seasonId, cancellationToken);

        // Season-end honours only once the final round is in.
        if (await roundRepository.IsLastRoundOfSeasonAsync(round.Id, seasonId, cancellationToken))
            await AwardSeasonHonoursAsync(awarder, seasonId, awardedUtc, cancellationToken);

        return awarder.NewAwards;
    }

    /// <summary>
    /// Records badges and remembers the ones that were genuinely new. AwardAsync returns true only on
    /// a real insert, so the digest can be told exactly what the player earned this round - per-round,
    /// season and lifetime badges alike, which a WHERE RoundId query could not do since lifetime and
    /// season awards store a null RoundId.
    /// </summary>
    private sealed class BadgeAwarder(IUserBadgeRepository userBadgeRepository, CancellationToken cancellationToken)
    {
        public List<RoundBadgeAward> NewAwards { get; } = [];

        public async Task AwardAsync(string userId, string badgeKey, DateTime whenUtc, int? leagueId = null, int? roundId = null, int? seasonScopeId = null, string? detail = null)
        {
            var isNewAward = await userBadgeRepository.AwardAsync(
                AwardedBadge.Create(userId, badgeKey, whenUtc, leagueId, roundId, seasonScopeId, detail),
                cancellationToken);

            if (isNewAward)
                NewAwards.Add(new RoundBadgeAward(userId, badgeKey));
        }
    }

    /// <summary>First steps and Sharpshooter, from each player's result in this round.</summary>
    private async Task AwardRoundResultBadgesAsync(BadgeAwarder awarder, Round round, DateTime awardedUtc, CancellationToken cancellationToken)
    {
        var results = await evaluationRepository.GetRoundResultsAsync(round.Id, cancellationToken);

        foreach (var r in results)
        {
            await awarder.AwardAsync(r.UserId, BadgeKeys.OffTheMark, awardedUtc);

            // Scored points this round = got at least one result right (TotalPoints is unreliable).
            if (r.ExactScoreCount > 0 || r.CorrectResultCount > 0)
                await awarder.AwardAsync(r.UserId, BadgeKeys.OnTheBoard, awardedUtc);

            if (r.ExactScoreCount >= 1)
                await awarder.AwardAsync(r.UserId, BadgeKeys.FirstBlood, awardedUtc);

            await AwardSharpshooterTiersAsync(awarder, r.UserId, r.ExactScoreCount, round.Id, awardedUtc);
        }
    }

    /// <summary>
    /// Every Sharpshooter tier the round's exact-score count reaches, awarded from the top down so a
    /// player who jumps several levels at once collects them all.
    /// </summary>
    private static async Task AwardSharpshooterTiersAsync(BadgeAwarder awarder, string userId, int exactScoreCount, int roundId, DateTime awardedUtc)
    {
        var detail = $"{exactScoreCount} in a round";

        if (exactScoreCount >= 5)
            await awarder.AwardAsync(userId, BadgeKeys.Sharpshooter3, awardedUtc, roundId: roundId, detail: detail);
        if (exactScoreCount >= 4)
            await awarder.AwardAsync(userId, BadgeKeys.Sharpshooter2, awardedUtc, roundId: roundId, detail: detail);
        if (exactScoreCount >= 3)
            await awarder.AwardAsync(userId, BadgeKeys.Sharpshooter1, awardedUtc, roundId: roundId, detail: detail);
    }

    /// <summary>
    /// Marksman (cumulative exact scores this season) and On Fire (consecutive rounds with an exact),
    /// both measured up to and including this round.
    /// </summary>
    private async Task AwardSeasonProgressBadgesAsync(BadgeAwarder awarder, Round round, int seasonId, DateTime awardedUtc, CancellationToken cancellationToken)
    {
        var cumulative = await evaluationRepository.GetSeasonCumulativeExactsAsync(seasonId, round.RoundNumber, cancellationToken);

        foreach (var c in cumulative)
        {
            await AwardMarksmanTiersAsync(awarder, c.UserId, c.Count, seasonId, awardedUtc);
        }

        var streaks = await evaluationRepository.GetStreaksEndingAtRoundAsync(seasonId, round.RoundNumber, cancellationToken);

        foreach (var s in streaks)
        {
            await AwardOnFireTiersAsync(awarder, s.UserId, s.Count, awardedUtc);
        }
    }

    /// <summary>Every Marksman tier the season's running exact-score total reaches.</summary>
    private static async Task AwardMarksmanTiersAsync(BadgeAwarder awarder, string userId, int count, int seasonId, DateTime awardedUtc)
    {
        var detail = $"{count} exact scores";

        if (count >= 15)
            await awarder.AwardAsync(userId, BadgeKeys.Marksman3, awardedUtc, seasonScopeId: seasonId, detail: detail);
        if (count >= 10)
            await awarder.AwardAsync(userId, BadgeKeys.Marksman2, awardedUtc, seasonScopeId: seasonId, detail: detail);
        if (count >= 5)
            await awarder.AwardAsync(userId, BadgeKeys.Marksman1, awardedUtc, seasonScopeId: seasonId, detail: detail);
    }

    /// <summary>Every On Fire tier the run of consecutive rounds with an exact score reaches.</summary>
    private static async Task AwardOnFireTiersAsync(BadgeAwarder awarder, string userId, int count, DateTime awardedUtc)
    {
        var detail = $"{count} in a row";

        if (count >= 7)
            await awarder.AwardAsync(userId, BadgeKeys.OnFire3, awardedUtc, detail: detail);
        if (count >= 5)
            await awarder.AwardAsync(userId, BadgeKeys.OnFire2, awardedUtc, detail: detail);
        if (count >= 3)
            await awarder.AwardAsync(userId, BadgeKeys.OnFire1, awardedUtc, detail: detail);
    }

    /// <summary>Round Winner, and Beat the Crowd for backing the minority result and winning.</summary>
    private async Task AwardRoundStandoutBadgesAsync(BadgeAwarder awarder, Round round, DateTime awardedUtc, CancellationToken cancellationToken)
    {
        var winners = await evaluationRepository.GetRoundWinnersAsync(round.Id, cancellationToken);
        foreach (var w in winners)
            await awarder.AwardAsync(w.UserId, BadgeKeys.RoundWinner, awardedUtc, leagueId: w.LeagueId, roundId: round.Id);

        var beatTheCrowd = await evaluationRepository.GetBeatTheCrowdUsersAsync(round.Id, MinimumCrowdForBeatTheCrowd, cancellationToken);
        foreach (var userId in beatTheCrowd)
            await awarder.AwardAsync(userId, BadgeKeys.BeatTheCrowd, awardedUtc, roundId: round.Id);
    }

    /// <summary>Leagues joined all-time, dated to when each Nth league was joined.</summary>
    private async Task AwardSocialiteBadgesAsync(BadgeAwarder awarder, CancellationToken cancellationToken)
    {
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
                await awarder.AwardAsync(s.UserId, badgeKey, s.AwardedUtc, detail: $"{s.Rank} leagues");
        }
    }

    /// <summary>
    /// Account and setup badges (add mobile, add bank details, create a league) for all users, dated
    /// to when they did it. Evaluated here (idempotent) so they land at the next round completion.
    /// </summary>
    private async Task AwardAccountBadgesAsync(BadgeAwarder awarder, CancellationToken cancellationToken)
    {
        var accountAwards = await evaluationRepository.GetAccountBadgeAwardsAsync(cancellationToken);

        foreach (var a in accountAwards)
            await awarder.AwardAsync(a.UserId, a.BadgeKey, a.AwardedUtc);
    }

    /// <summary>
    /// Month and stage winners for any period that is now fully complete. Repeatable, keyed by the
    /// period's final round and dated to it.
    /// </summary>
    private async Task AwardPeriodWinnerBadgesAsync(BadgeAwarder awarder, int seasonId, CancellationToken cancellationToken)
    {
        var monthWinners = await evaluationRepository.GetMonthWinnersAsync(seasonId, cancellationToken);
        foreach (var w in monthWinners)
            await awarder.AwardAsync(w.UserId, BadgeKeys.MonthWinner, w.AwardedUtc, leagueId: w.LeagueId, roundId: w.RoundId, detail: w.Detail);

        var stageWinners = await evaluationRepository.GetStageWinnersAsync(seasonId, cancellationToken);
        foreach (var w in stageWinners)
            await awarder.AwardAsync(w.UserId, BadgeKeys.StageWinner, w.AwardedUtc, leagueId: w.LeagueId, roundId: w.RoundId, detail: w.Detail);
    }

    /// <summary>Champion, Podium and Ever-Present, once the season's final round is complete.</summary>
    private async Task AwardSeasonHonoursAsync(BadgeAwarder awarder, int seasonId, DateTime awardedUtc, CancellationToken cancellationToken)
    {
        var standings = await evaluationRepository.GetSeasonStandingsAsync(seasonId, cancellationToken);

        foreach (var s in standings)
        {
            if (s.Rank == 1)
                await awarder.AwardAsync(s.UserId, BadgeKeys.Champion, awardedUtc, leagueId: s.LeagueId);
            if (s.Rank <= 3)
                await awarder.AwardAsync(s.UserId, BadgeKeys.Podium, awardedUtc, leagueId: s.LeagueId);
        }

        var everPresent = await evaluationRepository.GetEverPresentUsersAsync(seasonId, cancellationToken);
        foreach (var userId in everPresent)
            await awarder.AwardAsync(userId, BadgeKeys.EverPresent, awardedUtc, seasonScopeId: seasonId);
    }
}
