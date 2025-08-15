using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetWinningsQueryHandler : IRequestHandler<GetWinningsQuery, WinningsDto>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetWinningsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<WinningsDto> Handle(GetWinningsQuery request, CancellationToken cancellationToken)
    {
        var leagueData = await GetLeagueDataAsync(request.LeagueId, cancellationToken);
        if (leagueData == null)
            return new WinningsDto();

        if (leagueData.EntryDeadline > DateTime.Now || !leagueData.PrizeSettings.Any())
        {
            return new WinningsDto
            {
                WinningsCalculated = false,
                EntryCount = leagueData.EntryCount,
                EntryCost = leagueData.EntryCost,
                TotalPrizePot = leagueData.EntryCount * leagueData.EntryCost
            };
        }

        var winningsDto = new WinningsDto { WinningsCalculated = true };

        // Step 2: Process the prize lists (Rounds, Monthly, End of Season)
        ProcessRoundPrizes(winningsDto, leagueData);
        ProcessMonthlyPrizes(winningsDto, leagueData);
        ProcessEndOfSeasonPrizes(winningsDto, leagueData);

        // Step 3: Process the Winnings Leaderboard
        ProcessLeaderboard(winningsDto, leagueData);

        return winningsDto;
    }

    private void ProcessRoundPrizes(WinningsDto dto, LeagueData data)
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
                Winner = winner.WinnerName
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
                Winner = null
            });
        }
        dto.RoundPrizes = dto.RoundPrizes.OrderBy(p => int.Parse(p.Name)).ToList();
    }

    private void ProcessMonthlyPrizes(WinningsDto dto, LeagueData data)
    {
        var monthlyPrizeSetting = data.PrizeSettings.FirstOrDefault(p => p.PrizeType == PrizeType.Monthly);
        if (monthlyPrizeSetting == null)
            return;

        var seasonMonths = GetSeasonMonths(data.SeasonStartDate, data.SeasonEndDate);

        var wonMonthlyPrizes = data.Winnings
            .Where(w => w.PrizeType == PrizeType.Monthly)
            .Select(winner => new PrizeDto
            {
                Name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(winner.Month!.Value),
                Amount = winner.Amount,
                Winner = winner.WinnerName
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
                Winner = null
            });
        }

        dto.MonthlyPrizes = dto.MonthlyPrizes.OrderBy(p => {
            var monthNumber = DateTime.ParseExact(p.Name, "MMMM", CultureInfo.CurrentCulture).Month;
            var year = monthNumber < data.SeasonStartDate.Month ? data.SeasonStartDate.Year + 1 : data.SeasonStartDate.Year;
            return new DateTime(year, monthNumber, 1);
        }).ToList();
    }
    
    private void ProcessEndOfSeasonPrizes(WinningsDto dto, LeagueData data)
    {
        var specialPrizeSettings = data.PrizeSettings.Where(p => p.PrizeType != PrizeType.Round && p.PrizeType != PrizeType.Monthly);
     
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
                        Winner = winner.WinnerName
                    });
                }
            }
            else
            {
                dto.EndOfSeasonPrizes.Add(new PrizeDto
                {
                    Name = setting.Name,
                    Amount = setting.Amount,
                    Winner = null
                });
            }
        }
    }

    private void ProcessLeaderboard(WinningsDto dto, LeagueData data)
    {
        dto.Leaderboard.Entries = data.LeagueMembers
            .Select(member =>
            {
                // Find all winnings for the current member
                var memberWinnings = data.Winnings.Where(w => w.UserId == member.UserId).ToList();
                return new WinningsLeaderboardEntryDto
                {
                    PlayerName = member.PlayerName,
                    RoundWinnings = memberWinnings.Where(p => p.PrizeType == PrizeType.Round).Sum(p => p.Amount),
                    MonthlyWinnings = memberWinnings.Where(p => p.PrizeType == PrizeType.Monthly).Sum(p => p.Amount),
                    EndOfSeasonWinnings = memberWinnings.Where(p => p.PrizeType != PrizeType.Round && p.PrizeType != PrizeType.Monthly).Sum(p => p.Amount),
                    TotalWinnings = memberWinnings.Sum(p => p.Amount)
                };
            })
            .OrderByDescending(e => e.TotalWinnings)
            .ToList();
    }

    private async Task<LeagueData?> GetLeagueDataAsync(int leagueId, CancellationToken token)
    {
        const string leagueDataSql = @"
            SELECT 
                l.[EntryDeadline],
                l.[Price] AS [EntryCost],
                s.[StartDate] AS SeasonStartDate,
                s.[EndDate] AS SeasonEndDate,
                s.[NumberOfRounds] AS TotalRoundsInSeason,
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus) AS EntryCount
            FROM 
                [Leagues] l
            JOIN 
                [Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE 
                l.[Id] = @leagueId;";

        var leagueData = await _dbConnection.QuerySingleOrDefaultAsync<LeagueData>(leagueDataSql, token, new { leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
        if (leagueData == null) 
            return null;

        const string prizeSettingsSql = @"
            SELECT 
                [Id], 
                [PrizeType], 
                [PrizeDescription] AS [Name], 
                [PrizeAmount] AS [Amount]
            FROM 
                [LeaguePrizeSettings] 
            WHERE 
                [LeagueId] = @leagueId;";

        leagueData.PrizeSettings = (await _dbConnection.QueryAsync<PrizeSettingQueryResult>(prizeSettingsSql, token, new { leagueId })).ToList();

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

        leagueData.Winnings = (await _dbConnection.QueryAsync<WinningsQueryResult>(winningsSql, token, new { leagueId })).ToList();

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

        leagueData.LeagueMembers = (await _dbConnection.QueryAsync<LeagueMemberQueryResult>(membersSql, token, new { leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();

        return leagueData;
    }

    private IEnumerable<int> GetSeasonMonths(DateTime startDate, DateTime endDate)
    {
        for (var dt = startDate; dt <= endDate; dt = dt.AddMonths(1))
        {
            yield return dt.Month;
        }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private class LeagueData
    {
        public DateTime EntryDeadline { get; set; }
        public decimal EntryCost { get; set; }
        public int EntryCount { get; set; }
        public DateTime SeasonStartDate { get; set; }
        public DateTime SeasonEndDate { get; set; }
        public int TotalRoundsInSeason { get; set; }
        public List<PrizeSettingQueryResult> PrizeSettings { get; set; } = new();
        public List<WinningsQueryResult> Winnings { get; set; } = new();
        public List<LeagueMemberQueryResult> LeagueMembers { get; set; } = new();
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PrizeSettingQueryResult(int Id, PrizeType PrizeType, string Name, decimal Amount);
   
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record WinningsQueryResult(decimal Amount, int LeaguePrizeSettingId, PrizeType PrizeType, string WinnerName, int? RoundNumber, int? Month, string UserId);
   
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record LeagueMemberQueryResult(string PlayerName, string UserId);
}