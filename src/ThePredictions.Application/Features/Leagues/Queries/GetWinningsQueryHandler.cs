using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetWinningsQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetWinningsQuery, WinningsDto>
{
    public async Task<WinningsDto> Handle(GetWinningsQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        var leagueData = await GetLeagueDataAsync(request.LeagueId, cancellationToken);
        if (leagueData == null)
            return new WinningsDto();

        if (leagueData.EntryDeadlineUtc > dateTimeProvider.UtcNow || !leagueData.PrizeSettings.Any())
        {
            return new WinningsDto
            {
                WinningsCalculated = false,
                EntryCount = leagueData.EntryCount,
                EntryCost = leagueData.EntryCost,
                TotalPrizePot = leagueData.EntryCount * leagueData.EntryCost
            };
        }

        var winningsDto = new WinningsDto
        {
            WinningsCalculated = true,
            EntryCount = leagueData.EntryCount,
            EntryCost = leagueData.EntryCost,
            TotalPrizePot = leagueData.EntryCount * leagueData.EntryCost
        };

        ProcessRoundPrizes(winningsDto, leagueData);
        ProcessMonthlyPrizes(winningsDto, leagueData);
        ProcessStagePrizes(winningsDto, leagueData);
        ProcessEndOfSeasonPrizes(winningsDto, leagueData);
        ProcessLeaderboard(winningsDto, leagueData);

        return winningsDto;
    }

    private static void ProcessRoundPrizes(WinningsDto dto, LeagueData data)
    {
        var roundPrizeSetting = data.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Round);
        if (roundPrizeSetting == null) 
            return;
       
        var wonRoundPrizes = data.Winnings
            .Where(w => w.PrizeType == PrizeType.Round)
            .Select(winner => new PrizeDto
            {
                Name = winner.RoundNumber.ToString()!,
                Amount = winner.Amount,
                Winner = winner.WinnerName,
                UserId = winner.UserId
            });
      
        dto.RoundPrizes.AddRange(wonRoundPrizes);

        var wonRoundNumbers = data.Winnings.Where(w => w.PrizeType == PrizeType.Round).Select(w => w.RoundNumber).Distinct();
        var remainingRounds = Enumerable.Range(1, data.TotalRoundsInSeason).Where(r => !wonRoundNumbers.Contains(r));

        foreach (var roundNum in remainingRounds)
        {
            dto.RoundPrizes.Add(new PrizeDto
            {
                Name = roundNum.ToString(),
                Amount = roundPrizeSetting.Amount,
                Winner = null,
                UserId = null
            });
        }
        dto.RoundPrizes = dto.RoundPrizes.OrderBy(p => int.Parse(p.Name)).ToList();
    }

    private static void ProcessMonthlyPrizes(WinningsDto dto, LeagueData data)
    {
        var monthlyPrizeSetting = data.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Monthly);
        if (monthlyPrizeSetting == null)
            return;

        var seasonMonths = GetSeasonMonths(data.SeasonStartDateUtc, data.SeasonEndDateUtc);

        var wonMonthlyPrizes = data.Winnings
            .Where(w => w.PrizeType == PrizeType.Monthly)
            .Select(winner => new PrizeDto
            {
                Name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(winner.Month!.Value),
                Amount = winner.Amount,
                Winner = winner.WinnerName,
                UserId = winner.UserId
            });

        dto.MonthlyPrizes.AddRange(wonMonthlyPrizes);

        var wonMonths = data.Winnings.Where(w => w.PrizeType == PrizeType.Monthly).Select(w => w.Month).Distinct();
        var remainingMonths = seasonMonths.Where(m => !wonMonths.Contains(m));

        foreach (var monthNum in remainingMonths)
        {
            dto.MonthlyPrizes.Add(new PrizeDto
            {
                Name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(monthNum),
                Amount = monthlyPrizeSetting.Amount,
                Winner = null,
                UserId = null
            });
        }

        dto.MonthlyPrizes = dto.MonthlyPrizes.OrderBy(p => {
            var monthNumber = DateTime.ParseExact(p.Name, "MMMM", CultureInfo.CurrentCulture).Month;
            var year = monthNumber < data.SeasonStartDateUtc.Month ? data.SeasonStartDateUtc.Year + 1 : data.SeasonStartDateUtc.Year;
            return new DateTime(year, monthNumber, 1);
        }).ToList();
    }
    
    private static void ProcessStagePrizes(WinningsDto dto, LeagueData data)
    {
        var stageSettings = data.PrizeSettings
            .Where(p => p.PrizeType == PrizeType.Stages)
            .OrderBy(p => p.Stage)
            .ThenByDescending(p => p.Amount);

        foreach (var setting in stageSettings)
        {
            var winners = data.Winnings
                .Where(w => w.LeaguePrizeSettingId == setting.Id)
                .ToList();

            if (winners.Any())
            {
                foreach (var winner in winners)
                {
                    dto.StagePrizes.Add(new PrizeDto
                    {
                        Name = setting.Name,
                        Amount = winner.Amount,
                        Winner = winner.WinnerName,
                        UserId = winner.UserId
                    });
                }
            }
            else
            {
                dto.StagePrizes.Add(new PrizeDto
                {
                    Name = setting.Name,
                    Amount = setting.Amount,
                    Winner = null,
                    UserId = null
                });
            }
        }
    }

    private static void ProcessEndOfSeasonPrizes(WinningsDto dto, LeagueData data)
    {
        var specialPrizeSettings = data.PrizeSettings.Where(p => p.PrizeType != PrizeType.Round && p.PrizeType != PrizeType.Monthly && p.PrizeType != PrizeType.Stages);

        foreach (var setting in specialPrizeSettings.OrderBy(p => p.PrizeType).ThenByDescending(p => p.Amount))
        {
            var winners = data.Winnings
                .Where(w => w.LeaguePrizeSettingId == setting.Id)
                .ToList();

            if (winners.Any())
            {
                foreach (var winner in winners)
                {
                    dto.EndOfSeasonPrizes.Add(new PrizeDto
                    {
                        Name = setting.Name,
                        Amount = winner.Amount,
                        Winner = winner.WinnerName,
                        UserId = winner.UserId
                    });
                }
            }
            else
            {
                dto.EndOfSeasonPrizes.Add(new PrizeDto
                {
                    Name = setting.Name,
                    Amount = setting.Amount,
                    Winner = null,
                    UserId = null
                });
            }
        }
    }

    private static void ProcessLeaderboard(WinningsDto dto, LeagueData data)
    {
        dto.Leaderboard.Entries = data.LeagueMembers
            .Select(member =>
            {
                var memberWinnings = data.Winnings.Where(w => w.UserId == member.UserId).ToList();
                return new WinningsLeaderboardEntryDto
                {
                    PlayerName = member.PlayerName,
                    RoundWinnings = memberWinnings.Where(p => p.PrizeType == PrizeType.Round).Sum(p => p.Amount),
                    MonthlyWinnings = memberWinnings.Where(p => p.PrizeType == PrizeType.Monthly).Sum(p => p.Amount),
                    StageWinnings = memberWinnings.Where(p => p.PrizeType == PrizeType.Stages).Sum(p => p.Amount),
                    EndOfSeasonWinnings = memberWinnings.Where(p => p.PrizeType != PrizeType.Round && p.PrizeType != PrizeType.Monthly && p.PrizeType != PrizeType.Stages).Sum(p => p.Amount),
                    TotalWinnings = memberWinnings.Sum(p => p.Amount),
                    UserId = member.UserId
                };
            })
            .OrderByDescending(e => e.TotalWinnings)
            .ThenBy(e => e.PlayerName)
            .ToList();
    }

    private async Task<LeagueData?> GetLeagueDataAsync(int leagueId, CancellationToken token)
    {
        const string leagueDataSql = @"
            SELECT 
                l.[EntryDeadlineUtc],
                l.[Price] AS [EntryCost],
                s.[StartDateUtc] AS SeasonStartDateUtc,
                s.[EndDateUtc] AS SeasonEndDateUtc,
                s.[NumberOfRounds] AS TotalRoundsInSeason,
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus) AS EntryCount
            FROM 
                [Leagues] l
            JOIN 
                [Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE 
                l.[Id] = @leagueId;";

        var leagueData = await dbConnection.QuerySingleOrDefaultAsync<LeagueData>(leagueDataSql, token, new { leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
        if (leagueData == null) 
            return null;

        const string prizeSettingsSql = @"
            SELECT
                [Id],
                [PrizeType],
                [PrizeDescription] AS [Name],
                [PrizeAmount] AS [Amount],
                [Stage]
            FROM
                [LeaguePrizeSettings]
            WHERE
                [LeagueId] = @leagueId;";

        leagueData.PrizeSettings = (await dbConnection.QueryAsync<PrizeSettingQueryResult>(prizeSettingsSql, token, new { leagueId })).ToList();

        const string winningsSql = @"
            SELECT 
                w.[Amount],
                w.[LeaguePrizeSettingId],
                lps.[PrizeType],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS WinnerName,
                w.[RoundNumber],
                w.[Month],
                w.[UserId]
                
            FROM 
                [Winnings] w
            JOIN 
                [LeaguePrizeSettings] lps ON w.[LeaguePrizeSettingId] = lps.[Id]
            JOIN 
                [AspNetUsers] u ON w.[UserId] = u.[Id]
            WHERE 
                lps.[LeagueId] = @leagueId;";

        leagueData.Winnings = (await dbConnection.QueryAsync<WinningsQueryResult>(winningsSql, token, new { leagueId })).ToList();

        const string membersSql = @"
            SELECT
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                u.[Id] AS UserId
            FROM 
                [LeagueMembers] lm
            JOIN 
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE 
                lm.[LeagueId] = @leagueId
                AND lm.[Status] = @ApprovedStatus";

        leagueData.LeagueMembers = (await dbConnection.QueryAsync<LeagueMemberQueryResult>(membersSql, token, new { leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();

        return leagueData;
    }

    private static IEnumerable<int> GetSeasonMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        for (var dt = startDateUtc; dt <= endDateUtc; dt = dt.AddMonths(1))
        {
            yield return dt.Month;
        }
    }

    // internal so a test can supply rows for the prize shaping above; InternalsVisibleTo already
    // exposes this assembly to ThePredictions.Application.Tests.Unit.
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal class LeagueData
    {
        public DateTime EntryDeadlineUtc { get; set; }
        public decimal EntryCost { get; set; }
        public int EntryCount { get; set; }
        public DateTime SeasonStartDateUtc { get; set; }
        public DateTime SeasonEndDateUtc { get; set; }
        public int TotalRoundsInSeason { get; set; }
        public List<PrizeSettingQueryResult> PrizeSettings { get; set; } = new();
        public List<WinningsQueryResult> Winnings { get; set; } = new();
        public List<LeagueMemberQueryResult> LeagueMembers { get; set; } = new();
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal record PrizeSettingQueryResult(int Id, PrizeType PrizeType, string Name, decimal Amount, string? Stage);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal record WinningsQueryResult(decimal Amount, int LeaguePrizeSettingId, PrizeType PrizeType, string WinnerName, int? RoundNumber, int? Month, string UserId);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal record LeagueMemberQueryResult(string PlayerName, string UserId);
}
