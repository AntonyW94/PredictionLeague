using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Services;

public class ReminderService(IApplicationReadDbConnection dbConnection) : IReminderService
{
    public Task<bool> ShouldSendReminderAsync(Round round, DateTime nowUtc)
    {
        var deadline = round.DeadlineUtc;
        var lastSent = round.LastReminderSentUtc;

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
            if (nowUtc >= targetTime)
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
                CASE
                    WHEN LEN(LTRIM(RTRIM(r.[DisplayName]))) > 0 THEN r.[DisplayName]
                    ELSE 'Round ' + CONVERT(NVARCHAR(MAX), r.[RoundNumber])
                END AS RoundName,
                r.[DeadlineUtc],
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

        return (await dbConnection.QueryAsync<ChaseUserDto>(sql, cancellationToken, new { RoundId = roundId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }
}