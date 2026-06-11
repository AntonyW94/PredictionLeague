using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.External.Tasks.Queries;

public class GetLeagueWelcomeBatchQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetLeagueWelcomeBatchQuery, IReadOnlyList<LeagueWelcomeLeague>>
{
    public async Task<IReadOnlyList<LeagueWelcomeLeague>> Handle(GetLeagueWelcomeBatchQuery request, CancellationToken cancellationToken)
    {
        // Column order must match the LeagueRecipientRow constructor (Dapper maps positionally).
        // One row per (league, unwelcomed approved member). Leagues with a scheme awaiting its
        // freeze are excluded so the welcome email always describes confirmed prizes.
        const string recipientsSql = @"
            SELECT
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                s.[Name] AS SeasonName,
                l.[HasPrizes],
                s.[NumberOfRounds],
                s.[StartDateUtc] AS SeasonStartDateUtc,
                s.[EndDateUtc] AS SeasonEndDateUtc,
                (SELECT COUNT(*) FROM [LeagueMembers] m WHERE m.[LeagueId] = l.[Id] AND m.[Status] = @ApprovedStatus) AS MemberCount,
                u.[Id] AS UserId,
                u.[Email],
                u.[FirstName]
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
            JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE
                l.[EntryDeadlineUtc] <= @NowUtc
                AND l.[EntryDeadlineUtc] >= @WindowStartUtc
                AND lm.[Status] = @ApprovedStatus
                AND NOT EXISTS
                (
                    SELECT
                        1
                    FROM
                        [LeagueWelcomeNotifications] lwn
                    WHERE
                        lwn.[LeagueId] = l.[Id]
                        AND lwn.[UserId] = lm.[UserId]
                )
                AND NOT EXISTS
                (
                    SELECT
                        1
                    FROM
                        [LeaguePrizeScheme] lpsc
                    WHERE
                        lpsc.[LeagueId] = l.[Id]
                        AND NOT EXISTS
                        (
                            SELECT
                                1
                            FROM
                                [LeaguePrizeSettings] lps
                            WHERE
                                lps.[LeagueId] = l.[Id]
                        )
                )
            ORDER BY
                l.[Id],
                u.[FirstName];";

        var recipientRows = (await dbConnection.QueryAsync<LeagueRecipientRow>(
            recipientsSql,
            cancellationToken,
            new { request.NowUtc, request.WindowStartUtc, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();

        if (recipientRows.Count == 0)
            return [];

        var leagueIds = recipientRows.Select(r => r.LeagueId).Distinct().ToList();

        const string prizesSql = @"
            SELECT
                lps.[LeagueId],
                lps.[PrizeType],
                lps.[Rank],
                lps.[Stage],
                lps.[PrizeAmount] AS Amount
            FROM
                [LeaguePrizeSettings] lps
            WHERE
                lps.[LeagueId] IN @LeagueIds;";

        var prizeRows = (await dbConnection.QueryAsync<PrizeRow>(prizesSql, cancellationToken, new { LeagueIds = leagueIds })).ToList();

        const string boostsSql = @"
            SELECT
                lbr.[Id] AS RuleId,
                lbr.[LeagueId],
                bd.[Name],
                bd.[Description],
                lbr.[TotalUsesPerSeason]
            FROM
                [LeagueBoostRules] lbr
            JOIN
                [BoostDefinitions] bd ON lbr.[BoostDefinitionId] = bd.[Id]
            WHERE
                lbr.[LeagueId] IN @LeagueIds
                AND lbr.[IsEnabled] = 1;";

        var boostRows = (await dbConnection.QueryAsync<BoostRow>(boostsSql, cancellationToken, new { LeagueIds = leagueIds })).ToList();

        const string windowsSql = @"
            SELECT
                lbw.[LeagueBoostRuleId],
                lbw.[StartRoundNumber],
                lbw.[EndRoundNumber],
                lbw.[MaxUsesInWindow]
            FROM
                [LeagueBoostWindows] lbw
            JOIN
                [LeagueBoostRules] lbr ON lbw.[LeagueBoostRuleId] = lbr.[Id]
            WHERE
                lbr.[LeagueId] IN @LeagueIds
                AND lbr.[IsEnabled] = 1;";

        var windowRows = (await dbConnection.QueryAsync<WindowRow>(windowsSql, cancellationToken, new { LeagueIds = leagueIds })).ToList();

        var prizesByLeague = prizeRows.ToLookup(p => p.LeagueId);
        var boostsByLeague = boostRows.ToLookup(b => b.LeagueId);
        var windowsByRule = windowRows.ToLookup(w => w.LeagueBoostRuleId);

        return recipientRows
            .GroupBy(r => r.LeagueId)
            .Select(group =>
            {
                var first = group.First();

                var prizes = prizesByLeague[group.Key]
                    .Select(p => new LeagueWelcomePrize(p.PrizeType, p.Rank, p.Stage, p.Amount))
                    .ToList();

                var boosts = boostsByLeague[group.Key]
                    .Select(b => new LeagueWelcomeBoost(
                        b.Name,
                        b.Description,
                        b.TotalUsesPerSeason,
                        windowsByRule[b.RuleId]
                            .OrderBy(w => w.StartRoundNumber)
                            .Select(w => new LeagueWelcomeBoostWindow(w.StartRoundNumber, w.EndRoundNumber, w.MaxUsesInWindow))
                            .ToList()))
                    .ToList();

                var recipients = group
                    .Select(r => new LeagueWelcomeRecipient(r.UserId, r.Email, r.FirstName))
                    .ToList();

                return new LeagueWelcomeLeague(
                    first.LeagueId,
                    first.LeagueName,
                    first.SeasonName,
                    first.HasPrizes,
                    first.MemberCount,
                    first.NumberOfRounds,
                    CountMonths(first.SeasonStartDateUtc, first.SeasonEndDateUtc),
                    prizes,
                    boosts,
                    recipients);
            })
            .ToList();
    }

    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;
        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record LeagueRecipientRow(
        int LeagueId,
        string LeagueName,
        string SeasonName,
        bool HasPrizes,
        int NumberOfRounds,
        DateTime SeasonStartDateUtc,
        DateTime SeasonEndDateUtc,
        int MemberCount,
        string UserId,
        string Email,
        string FirstName);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PrizeRow(int LeagueId, PrizeType PrizeType, int Rank, string? Stage, decimal Amount);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record BoostRow(int RuleId, int LeagueId, string Name, string? Description, int TotalUsesPerSeason);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record WindowRow(int LeagueBoostRuleId, int StartRoundNumber, int EndRoundNumber, int MaxUsesInWindow);
}
