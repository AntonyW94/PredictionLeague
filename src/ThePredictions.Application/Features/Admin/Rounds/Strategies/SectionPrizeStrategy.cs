using ThePredictions.Application.Common.Helpers;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Strategies;

/// <summary>
/// Awards Section prizes at the end of the season: members are ranked by their aggregate score
/// within each tournament stage (group stage vs knockouts) and paid per the frozen, stage-tagged
/// Section settings. Ties pool the covered slots, matching <see cref="OverallPrizeStrategy"/>.
/// </summary>
public class SectionPrizeStrategy(
    IWinningsRepository winningsRepository,
    IRoundRepository roundRepository,
    ITournamentRoundMappingRepository tournamentRoundMappingRepository,
    ILeagueRepository leagueRepository,
    IDateTimeProvider dateTimeProvider) : IPrizeStrategy
{
    private const string GroupStageName = "Group stage";
    private const string KnockoutStageName = "Knockout stage";

    public PrizeType PrizeType => PrizeType.Section;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var isLastRoundOfSeason = await roundRepository.IsLastRoundOfSeasonAsync(currentRound.Id, currentRound.SeasonId, cancellationToken);
        if (!isLastRoundOfSeason)
            return;

        var league = await leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);
        if (league == null)
            return;

        var sectionSettings = league.PrizeSettings.Where(p => p.PrizeType == PrizeType.Section).ToList();
        if (sectionSettings.Count == 0)
            return;

        var (groupRoundIds, knockoutRoundIds) = await ResolveStageRoundIdsAsync(currentRound.SeasonId, cancellationToken);

        await winningsRepository.DeleteWinningsForSectionAsync(league.Id, cancellationToken);

        var allNewWinnings = new List<Winning>();
        allNewWinnings.AddRange(AwardStage(league, sectionSettings, GroupStageName, groupRoundIds));
        allNewWinnings.AddRange(AwardStage(league, sectionSettings, KnockoutStageName, knockoutRoundIds));

        if (allNewWinnings.Any())
            await winningsRepository.AddWinningsAsync(allNewWinnings, cancellationToken);
    }

    private async Task<(List<int> GroupRoundIds, List<int> KnockoutRoundIds)> ResolveStageRoundIdsAsync(int seasonId, CancellationToken cancellationToken)
    {
        var mappings = await tournamentRoundMappingRepository.GetBySeasonIdAsync(seasonId, cancellationToken);
        var roundsById = await roundRepository.GetAllForSeasonAsync(seasonId, cancellationToken);
        var roundIdByNumber = roundsById.Values.ToDictionary(r => r.RoundNumber, r => r.Id);

        var groupRoundIds = new List<int>();
        var knockoutRoundIds = new List<int>();

        foreach (var mapping in mappings)
        {
            if (!roundIdByNumber.TryGetValue(mapping.RoundNumber, out var roundId))
                continue;

            if (IsGroupStage(mapping))
                groupRoundIds.Add(roundId);
            else
                knockoutRoundIds.Add(roundId);
        }

        return (groupRoundIds, knockoutRoundIds);
    }

    private static bool IsGroupStage(TournamentRoundMapping mapping) =>
        mapping.GetStageList().Any(s => s is TournamentStage.Group1 or TournamentStage.Group2 or TournamentStage.Group3);

    private List<Winning> AwardStage(League league, IReadOnlyList<LeaguePrizeSetting> sectionSettings, string stageName, IReadOnlyList<int> stageRoundIds)
    {
        var stagePrizeSettings = sectionSettings
            .Where(p => p.Stage == stageName)
            .OrderBy(p => p.Rank)
            .ToList();

        if (stagePrizeSettings.Count == 0 || stageRoundIds.Count == 0)
            return [];

        var rankings = league.GetStageRankings(stageRoundIds);
        var winnings = new List<Winning>();

        foreach (var rankingGroup in rankings)
        {
            var winners = rankingGroup.Members;
            if (winners.Count == 0)
                continue;

            // A joint group at rank R with N members occupies slots R..R+N-1; pool their prize money.
            var firstSlot = rankingGroup.Rank;
            var lastSlot = rankingGroup.Rank + winners.Count - 1;

            var coveredSettings = stagePrizeSettings.Where(p => p.Rank >= firstSlot && p.Rank <= lastSlot).ToList();
            if (coveredSettings.Count == 0)
                continue;

            var pooled = coveredSettings.Sum(p => p.PrizeAmount);
            if (pooled == 0)
                continue;

            var anchor = coveredSettings[0];
            var shares = PrizeDistributionHelper.DistributePrizeMoney(pooled, winners.Count);

            for (var i = 0; i < winners.Count; i++)
                winnings.Add(Winning.Create(winners[i].UserId, anchor.Id, shares[i], null, null, dateTimeProvider));
        }

        return winnings;
    }
}
