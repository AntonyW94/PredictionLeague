using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class LeagueMemberRepository(IDbConnectionFactory connectionFactory) : ILeagueMemberRepository
{
    private IDbConnection Connection => connectionFactory.CreateConnection();
    
    public async Task<LeagueMember?> GetAsync(int leagueId, string userId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [LeagueId],
                [UserId],
                [Status],
                [IsAlertDismissed],
                [JoinedAtUtc],
                [ApprovedAtUtc]
            FROM
                [LeagueMembers]
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId, UserId = userId }, cancellationToken: cancellationToken);
        return await Connection.QueryFirstOrDefaultAsync<LeagueMember>(command); 
    }

    public async Task UpdateAsync(LeagueMember member, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [LeagueMembers]
            SET
                [Status] = @Status,
                [IsAlertDismissed] = @IsAlertDismissed,
                [ApprovedAtUtc] = @ApprovedAtUtc
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId";

        var command = new CommandDefinition(sql, new
        {
            Status = member.Status.ToString(),
            member.IsAlertDismissed,
            member.ApprovedAtUtc,
            member.LeagueId,
            member.UserId
        }, cancellationToken: cancellationToken);
        
        await Connection.ExecuteAsync(command); 
    }

    public async Task DeleteAsync(LeagueMember member, CancellationToken cancellationToken)
    {
        const string sql = @"
            DELETE FROM
                [LeagueMembers]
            WHERE
                [LeagueId] = @LeagueId
                AND [UserId] = @UserId";
     
        var command = new CommandDefinition(sql, new
        {
            member.LeagueId,
            member.UserId
        }, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}