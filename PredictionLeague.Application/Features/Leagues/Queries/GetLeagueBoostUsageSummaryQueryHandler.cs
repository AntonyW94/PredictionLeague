using System.Diagnostics.CodeAnalysis;
using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Boosts;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetLeagueBoostUsageSummaryQueryHandler
    : IRequestHandler<GetLeagueBoostUsageSummaryQuery, List<BoostUsageSummaryDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;
    private readonly ILeagueMembershipService _membershipService;

    public GetLeagueBoostUsageSummaryQueryHandler(
        IApplicationReadDbConnection dbConnection,
        ILeagueMembershipService membershipService)
    {
        _dbConnection = dbConnection;
        _membershipService = membershipService;
    }

    public async Task<List<BoostUsageSummaryDto>> Handle(
        GetLeagueBoostUsageSummaryQuery request,
        CancellationToken cancellationToken)
    {
        await _membershipService.EnsureApprovedMemberAsync(
            request.LeagueId, request.CurrentUserId, cancellationToken);

        var boostRulesTask = GetEnabledBoostRulesAsync(request.LeagueId, cancellationToken);
        var windowsTask = GetWindowsAsync(request.LeagueId, cancellationToken);
        var membersTask = GetMembersAsync(request.LeagueId, cancellationToken);
        var seasonInfoTask = GetSeasonInfoAsync(request.LeagueId, cancellationToken);

        await Task.WhenAll(boostRulesTask, windowsTask, membersTask, seasonInfoTask);

        var boostRules = boostRulesTask.Result.ToList();
        if (boostRules.Count == 0)
            return [];

        var windows = windowsTask.Result.ToList();
        var members = membersTask.Result.ToList();
        var seasonInfo = seasonInfoTask.Result;

        if (seasonInfo == null)
            return [];

        var usages = (await GetUsagesAsync(
            request.LeagueId, seasonInfo.SeasonId, request.CurrentUserId, cancellationToken)).ToList();

        var roundRange = await GetRoundRangeAsync(request.LeagueId, cancellationToken);

        var result = new List<BoostUsageSummaryDto>();

        foreach (var rule in boostRules)
        {
            var ruleWindows = windows
                .Where(w => w.LeagueBoostRuleId == rule.LeagueBoostRuleId)
                .OrderBy(w => w.StartRoundNumber)
                .ToList();

            var boostUsages = usages.Where(u => u.BoostCode == rule.BoostCode).ToList();

            var windowDtos = new List<WindowUsageSummaryDto>();

            if (ruleWindows.Count == 0)
            {
                var isFullSeason = true;
                var maxUses = rule.TotalUsesPerSeason;

                var playerUsages = BuildPlayerUsages(
                    members, boostUsages, null, null, maxUses, request.CurrentUserId);

                windowDtos.Add(new WindowUsageSummaryDto
                {
                    StartRoundNumber = roundRange?.MinRoundNumber ?? 1,
                    EndRoundNumber = roundRange?.MaxRoundNumber ?? 1,
                    MaxUsesInWindow = maxUses,
                    IsFullSeason = isFullSeason,
                    PlayerUsages = playerUsages
                });
            }
            else
            {
                var isFullSeason = ruleWindows.Count == 1
                    && roundRange != null
                    && ruleWindows[0].StartRoundNumber <= roundRange.MinRoundNumber
                    && ruleWindows[0].EndRoundNumber >= roundRange.MaxRoundNumber;

                foreach (var window in ruleWindows)
                {
                    var playerUsages = BuildPlayerUsages(
                        members, boostUsages,
                        window.StartRoundNumber, window.EndRoundNumber,
                        window.MaxUsesInWindow, request.CurrentUserId);

                    windowDtos.Add(new WindowUsageSummaryDto
                    {
                        StartRoundNumber = window.StartRoundNumber,
                        EndRoundNumber = window.EndRoundNumber,
                        MaxUsesInWindow = window.MaxUsesInWindow,
                        IsFullSeason = isFullSeason,
                        PlayerUsages = playerUsages
                    });
                }
            }

            result.Add(new BoostUsageSummaryDto
            {
                BoostCode = rule.BoostCode,
                Name = rule.Name,
                ImageUrl = rule.ImageUrl,
                TotalUsesPerSeason = rule.TotalUsesPerSeason,
                Windows = windowDtos
            });
        }

        return result;
    }

    private static List<PlayerWindowUsageDto> BuildPlayerUsages(
        List<MemberRow> members,
        List<UsageRow> boostUsages,
        int? startRound,
        int? endRound,
        int maxUses,
        string currentUserId)
    {
        return members.Select(member =>
        {
            var memberUsages = boostUsages
                .Where(u => u.UserId == member.UserId);

            if (startRound.HasValue && endRound.HasValue)
            {
                memberUsages = memberUsages
                    .Where(u => u.RoundNumber >= startRound.Value && u.RoundNumber <= endRound.Value);
            }

            var usageList = memberUsages.ToList();
            var usedCount = usageList.Count;
            var remaining = Math.Max(0, maxUses - usedCount);

            return new PlayerWindowUsageDto
            {
                UserId = member.UserId,
                PlayerName = member.PlayerName,
                Remaining = remaining,
                MaxUses = maxUses,
                IsCurrentUser = member.UserId == currentUserId,
                RoundsUsedIn = usageList.Select(u => u.RoundNumber).OrderBy(r => r).ToList()
            };
        }).ToList();
    }

    private async Task<IEnumerable<BoostRuleRow>> GetEnabledBoostRulesAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                bd.[Code] AS [BoostCode],
                bd.[Name],
                bd.[ImageUrl],
                lbr.[TotalUsesPerSeason],
                lbr.[Id] AS [LeagueBoostRuleId]
            FROM [BoostDefinitions] bd
            INNER JOIN [LeagueBoostRules] lbr ON lbr.[BoostDefinitionId] = bd.[Id]
            WHERE lbr.[LeagueId] = @LeagueId AND lbr.[IsEnabled] = 1
            ORDER BY lbr.[Id];";

        return await _dbConnection.QueryAsync<BoostRuleRow>(sql, cancellationToken, new { LeagueId = leagueId });
    }

    private async Task<IEnumerable<WindowRow>> GetWindowsAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lbw.[LeagueBoostRuleId],
                lbw.[StartRoundNumber],
                lbw.[EndRoundNumber],
                lbw.[MaxUsesInWindow]
            FROM [LeagueBoostWindows] lbw
            INNER JOIN [LeagueBoostRules] lbr ON lbw.[LeagueBoostRuleId] = lbr.[Id]
            WHERE lbr.[LeagueId] = @LeagueId AND lbr.[IsEnabled] = 1
            ORDER BY lbw.[StartRoundNumber];";

        return await _dbConnection.QueryAsync<WindowRow>(sql, cancellationToken, new { LeagueId = leagueId });
    }

    private async Task<IEnumerable<MemberRow>> GetMembersAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id] AS [UserId],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName]
            FROM [LeagueMembers] lm
            JOIN [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE lm.[LeagueId] = @LeagueId AND lm.[Status] = @ApprovedStatus
            ORDER BY [PlayerName];";

        return await _dbConnection.QueryAsync<MemberRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
    }

    private async Task<SeasonInfoRow?> GetSeasonInfoAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId;";

        return await _dbConnection.QuerySingleOrDefaultAsync<SeasonInfoRow>(
            sql, cancellationToken, new { LeagueId = leagueId });
    }

    private async Task<IEnumerable<UsageRow>> GetUsagesAsync(
        int leagueId, int seasonId, string currentUserId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ubu.[UserId],
                bd.[Code] AS [BoostCode],
                r.[RoundNumber]
            FROM [UserBoostUsages] ubu
            INNER JOIN [BoostDefinitions] bd ON ubu.[BoostDefinitionId] = bd.[Id]
            INNER JOIN [Rounds] r ON ubu.[RoundId] = r.[Id]
            WHERE ubu.[LeagueId] = @LeagueId
              AND ubu.[SeasonId] = @SeasonId
              AND (
                  ubu.[UserId] = @CurrentUserId
                  OR r.[DeadlineUtc] <= GETUTCDATE()
              )
            ORDER BY r.[RoundNumber];";

        return await _dbConnection.QueryAsync<UsageRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, SeasonId = seasonId, CurrentUserId = currentUserId });
    }

    private async Task<RoundRangeRow?> GetRoundRangeAsync(
        int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                MIN(r.[RoundNumber]) AS [MinRoundNumber],
                MAX(r.[RoundNumber]) AS [MaxRoundNumber]
            FROM [Rounds] r
            INNER JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
            WHERE l.[Id] = @LeagueId;";

        return await _dbConnection.QuerySingleOrDefaultAsync<RoundRangeRow>(
            sql, cancellationToken, new { LeagueId = leagueId });
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class BoostRuleRow
    {
        public string BoostCode { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? ImageUrl { get; init; }
        public int TotalUsesPerSeason { get; init; }
        public int LeagueBoostRuleId { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class WindowRow
    {
        public int LeagueBoostRuleId { get; init; }
        public int StartRoundNumber { get; init; }
        public int EndRoundNumber { get; init; }
        public int MaxUsesInWindow { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class MemberRow
    {
        public string UserId { get; init; } = string.Empty;
        public string PlayerName { get; init; } = string.Empty;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class SeasonInfoRow
    {
        public int SeasonId { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class UsageRow
    {
        public string UserId { get; init; } = string.Empty;
        public string BoostCode { get; init; } = string.Empty;
        public int RoundNumber { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class RoundRangeRow
    {
        public int MinRoundNumber { get; init; }
        public int MaxRoundNumber { get; init; }
    }
}
