using ThePredictions.Domain.Services.Prizes;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Strategies;

/// <summary>
/// Awards Section prizes as soon as a tournament stage finishes: once every round in a stage
/// (group stage vs knockouts) is completed, members are ranked by their aggregate score within
/// that stage and paid per the frozen, stage-tagged Section settings. Re-processing a round
/// re-awards its stage idempotently. Ties pool the covered slots, matching
/// <see cref="OverallPrizeStrategy"/>.
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

    public PrizeType PrizeType => PrizeType.Stages;

    public async Task AwardPrizes(ProcessPrizesCommand command, CancellationToken cancellationToken)
    {
        var currentRound = await roundRepository.GetByIdAsync(command.RoundId, cancellationToken);
        if (currentRound == null)
            return;

        var league = await leagueRepository.GetByIdWithAllDataAsync(command.LeagueId, cancellationToken);
        if (league == null)
            return;

        var sectionSettings = league.PrizeSettings.Where(p => p.PrizeType == PrizeType.Stages).ToList();
        if (sectionSettings.Count == 0)
            return;

        var stages = await ResolveStagesAsync(currentRound.SeasonId, cancellationToken);

        foreach (var stage in stages.Where(s => s.IsComplete))
        {
            var newWinnings = AwardStage(league, sectionSettings, stage.Name, stage.RoundIds);

            await winningsRepository.DeleteWinningsForStageAsync(league.Id, stage.Name, cancellationToken);

            if (newWinnings.Any())
                await winningsRepository.AddWinningsAsync(newWinnings, cancellationToken);
        }
    }

    private async Task<List<StageRounds>> ResolveStagesAsync(int seasonId, CancellationToken cancellationToken)
    {
        var mappings = await tournamentRoundMappingRepository.GetBySeasonIdAsync(seasonId, cancellationToken);
        var roundsById = await roundRepository.GetAllForSeasonAsync(seasonId, cancellationToken);
        var roundByNumber = roundsById.Values.ToDictionary(r => r.RoundNumber);

        var groupStage = new StageRounds(GroupStageName);
        var knockoutStage = new StageRounds(KnockoutStageName);

        foreach (var mapping in mappings)
        {
            if (!roundByNumber.TryGetValue(mapping.RoundNumber, out var round))
                continue;

            if (IsGroupStage(mapping))
                groupStage.Add(round);
            else
                knockoutStage.Add(round);
        }

        return [groupStage, knockoutStage];
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
            // Always at least one member: rankings come from a GroupBy, so no empty-group guard.
            var winners = rankingGroup.Members;

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
            var shares = SharedPrizeSplitter.Split(pooled, winners.Count);

            for (var i = 0; i < winners.Count; i++)
                winnings.Add(Winning.Create(winners[i].UserId, anchor.Id, shares[i], null, null, dateTimeProvider));
        }

        return winnings;
    }

    /// <summary>The rounds that make up one tournament stage; complete once every round has finished.</summary>
    private sealed class StageRounds(string name)
    {
        private readonly List<Round> _rounds = [];

        public string Name { get; } = name;
        public IReadOnlyList<int> RoundIds => _rounds.Select(r => r.Id).ToList();
        public bool IsComplete => _rounds.Count > 0 && _rounds.All(r => r.Status == RoundStatus.Completed);

        public void Add(Round round) => _rounds.Add(round);
    }
}
