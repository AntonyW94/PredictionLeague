using ThePredictions.Domain.Services;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Assembles the live prize-evaluation inputs for a league: the pot context, the season's shape, and the scheme if it has one.
/// </summary>
public class PrizeEvaluationInputsReader(IPrizeEvaluationInputsQuery inputsQuery) : IPrizeEvaluationInputsReader
{
    public async Task<PrizeEvaluationInputs?> LoadAsync(int leagueId, CancellationToken cancellationToken) =>
        Build(await inputsQuery.GetByLeagueIdAsync(leagueId, cancellationToken));

    public async Task<PrizeEvaluationInputs?> LoadByEntryCodeAsync(string entryCode, CancellationToken cancellationToken) =>
        Build(await inputsQuery.GetByEntryCodeAsync(entryCode, cancellationToken));

    private static PrizeEvaluationInputs? Build(PrizeEvaluationInputsData? data)
    {
        if (data is null)
            return null;

        var league = data.League;

        return new PrizeEvaluationInputs
        {
            LeagueId = league.LeagueId,
            LeagueName = league.LeagueName,
            SeasonName = league.SeasonName,

            // The administrator is shown the way players are shown to each other everywhere, which was the last copy but one of
            // this rule written out in SQL.
            AdministratorName = PlayerDisplayName.Format(league.AdministratorFirstName, league.AdministratorLastName),
            AdministratorUserId = league.AdministratorUserId,
            EntryCode = league.EntryCode,
            EntryCost = league.EntryCost,
            EntrantCount = league.EntrantCount,
            EntryDeadlineUtc = league.EntryDeadlineUtc,
            NumberOfRounds = league.NumberOfRounds,
            NumberOfMonths = CountMonths(league.SeasonStartDateUtc, league.SeasonEndDateUtc),

            // A league has a scheme when a scheme row exists for it, which is what decides whether the per-category amounts below
            // mean anything.
            HasScheme = data.Schemes.Count > 0,

            // Whole pounds: the evaluator works in them, and a top-up of nothing is the same as no top-up.
            AdminTopUpPounds = (int)decimal.Truncate(league.PrizeFundOverride ?? 0m),
            Categories = data.Entries
                .Select(entry => new PrizeSchemeCategoryInput
                {
                    Category = entry.Category,
                    PerEntryPounds = entry.PerEntryPounds,
                    RankTableJson = entry.RankTableJson
                })
                .ToList()
        };
    }

    /// <summary>
    /// How many calendar months the season touches, counted inclusively from its start.
    /// </summary>
    /// <remarks>
    /// Every month the season runs through gets a monthly prize, including a first or last month it only partly covers - which is
    /// why this steps month by month rather than subtracting the dates.
    /// </remarks>
    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;

        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }
}
