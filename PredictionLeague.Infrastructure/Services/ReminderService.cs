using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Admin.Users;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public ReminderService(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public Task<bool> ShouldSendReminderAsync(Round round, DateTime now, CancellationToken cancellationToken)
    {
        var deadline = round.Deadline;
        var lastSent = round.LastReminderSent;

        var milestones = new[]
        {
            deadline.AddDays(-5),  
            deadline.AddDays(-3), 
            deadline.AddDays(-1),  
            deadline.AddHours(-6),
            deadline.AddHours(-1)
        };

        foreach (var targetTime in milestones.OrderByDescending(m => m))
        {
            if (now >= targetTime)
                return Task.FromResult(lastSent == null || lastSent < targetTime);
        }

        return Task.FromResult(false);
    }

    public async Task<List<ChaseUserDto>> GetUsersMissingPredictionsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT DISTINCT
                u.[Email],
                u.[FirstName],
                'Round ' + CONVERT(NVARCHAR(MAX), r.[RoundNumber]) AS RoundName,
                r.[Deadline],
                u.[Id] AS UserId
            FROM 
                [AspNetUsers] u
            JOIN 
                [LeagueMembers] lm ON u.[Id] = lm.[UserId]
            JOIN 
                [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN 
                [Rounds] r ON l.[SeasonId] = r.[SeasonId]
            WHERE 
                r.[Id] = @RoundId
                AND lm.[Status] = @ApprovedStatus
                AND NOT EXISTS (
                    SELECT 1 FROM [UserPredictions] up
                    JOIN [Matches] m ON up.[MatchId] = m.[Id]
                    WHERE m.[RoundId] = r.[Id] AND up.[UserId] = u.[Id]
              );";

        return (await _dbConnection.QueryAsync<ChaseUserDto>(sql, cancellationToken, new { RoundId = roundId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}