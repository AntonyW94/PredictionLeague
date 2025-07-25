using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class LeagueRepository : ILeagueRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    private const string GetLeaguesWithMembersSql = @"
        SELECT 
            l.*, 
            lm.*
        FROM [dbo].[Leagues] l
        LEFT JOIN [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]";

    public LeagueRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create

    public async Task<League> CreateAsync(League league, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [dbo].[Leagues] 
            (
                [Name], 
                [SeasonId], 
                [Price], 
                [AdministratorUserId], 
                [EntryCode], 
                [CreatedAt], 
                [EntryDeadline]
            )
            VALUES 
            (
                @Name, 
                @SeasonId, 
                @Price, 
                @AdministratorUserId, 
                @EntryCode, 
                @CreatedAt, 
                @EntryDeadline
            );
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: league,
            cancellationToken: cancellationToken
        );

        var newLeagueId = await Connection.ExecuteScalarAsync<int>(command);
        typeof(League).GetProperty(nameof(League.Id))?.SetValue(league, newLeagueId);
       
        var adminMember = LeagueMember.Create(newLeagueId, league.AdministratorUserId);
        adminMember.Approve();
        await AddMemberAsync(adminMember, cancellationToken);

        return league;
    }

    private Task AddMemberAsync(LeagueMember member, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [dbo].[LeagueMembers] ([LeagueId], [UserId], [Status], [JoinedAt], [ApprovedAt])
            VALUES (@LeagueId, @UserId, @Status, @JoinedAt, @ApprovedAt);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                member.LeagueId,
                member.UserId,
                Status = member.Status.ToString(),
                member.JoinedAt,
                member.ApprovedAt
            },
            cancellationToken: cancellationToken
        );

        return Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<League?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = $"{GetLeaguesWithMembersSql} WHERE l.[Id] = @Id;";

        return (await QueryAndMapLeagues(sql, cancellationToken, new { Id = id })).FirstOrDefault();
    }

    public async Task<League?> GetByEntryCodeAsync(string? entryCode, CancellationToken cancellationToken)
    {
        const string sql = $"{GetLeaguesWithMembersSql} WHERE l.[EntryCode] = @EntryCode;";

        return (await QueryAndMapLeagues(sql, cancellationToken, new { EntryCode = entryCode })).FirstOrDefault();
    }

    public async Task<IEnumerable<League>> GetLeaguesForScoringAsync(int seasonId, int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT
            l.*, 
            lm.*, 
            up.*
        FROM 
            [dbo].[Leagues] l
        INNER JOIN 
            [dbo].[LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
        LEFT JOIN 
            [dbo].[UserPredictions] up ON lm.[UserId] = up.[UserId] AND up.[MatchId] IN (SELECT Id FROM Matches WHERE RoundId = @RoundId)
        WHERE 
            l.[SeasonId] = @SeasonId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SeasonId = seasonId, RoundId = roundId },
            cancellationToken: cancellationToken
        );

        var queryResult = await Connection.QueryAsync<League, LeagueMember, UserPrediction, (League, LeagueMember, UserPrediction?)>(
            command,
            (league, member, prediction) => (league, member, prediction),
            splitOn: "LeagueId,Id"
        );

        var groupedLeagues = queryResult.GroupBy(x => x.Item1.Id).Select(leagueGroup =>
        {
            var firstLeague = leagueGroup.First().Item1;
            var members = leagueGroup.GroupBy(x => new { x.Item2.LeagueId, x.Item2.UserId }).Select(memberGroup =>
            {
                var firstMember = memberGroup.First().Item2;
                var predictions = memberGroup.Select(x => x.Item3).Where(p => p != null).ToList();
                return new LeagueMember(firstMember.LeagueId, firstMember.UserId, firstMember.Status, firstMember.JoinedAt, firstMember.ApprovedAt, predictions);
            }).ToList();

            return new League(
                firstLeague.Id,
                firstLeague.Name,
                firstLeague.Price,
                firstLeague.SeasonId,
                firstLeague.AdministratorUserId,
                firstLeague.EntryCode,
                firstLeague.CreatedAt,
                firstLeague.EntryDeadline,
                members,
                null
            );
        });

        return groupedLeagues;
    }

    public async Task<bool> DoesEntryCodeExistAsync(string entryCode, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT 
                COUNT(1) 
            FROM 
                [dbo].[Leagues] 
            WHERE 
                [EntryCode] = @EntryCode;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { EntryCode = entryCode },
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<int>(command) > 0;
    }

    #endregion

    #region Update
    public async Task UpdateAsync(League league, CancellationToken cancellationToken)
    {
        const string updateLeagueSql = @"
            UPDATE [dbo].[Leagues]
            SET [Name] = @Name,
                [Price] = @Price,
                [EntryCode] = @EntryCode,
                [EntryDeadline] = @EntryDeadline
            WHERE [Id] = @Id;";

        var leagueCommand = new CommandDefinition(
            updateLeagueSql,
            league,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(leagueCommand);

        const string deleteMembersSql = "DELETE FROM [dbo].[LeagueMembers] WHERE [LeagueId] = @LeagueId;";

        var deleteCommand = new CommandDefinition(
            deleteMembersSql,
            new { LeagueId = league.Id },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(deleteCommand);

        if (league.Members.Any())
        {
            const string insertMemberSql = @"
                INSERT INTO [dbo].[LeagueMembers] ([LeagueId], [UserId], [Status], [JoinedAt], [ApprovedAt])
                VALUES (@LeagueId, @UserId, @Status, @JoinedAt, @ApprovedAt);";

            var insertCommand = new CommandDefinition(insertMemberSql, league.Members.Select(m => new
            {
                m.LeagueId,
                m.UserId,
                Status = m.Status.ToString(),
                m.JoinedAt,
                m.ApprovedAt
            }), cancellationToken: cancellationToken);

            await Connection.ExecuteAsync(insertCommand);
        }
    }

    public async Task UpdateMemberStatusAsync(int leagueId, string userId, LeagueMemberStatus status, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE [dbo].[LeagueMembers]
            SET [Status] = @Status,
                [ApprovedAt] = CASE WHEN @Status = 'Approved' THEN GETUTCDATE() ELSE [ApprovedAt] END
            WHERE [LeagueId] = @LeagueId AND [UserId] = @UserId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Status = status.ToString(), LeagueId = leagueId, UserId = userId },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdatePredictionPointsAsync(IEnumerable<UserPrediction> predictionsToUpdate, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE [dbo].[UserPredictions]
            SET [PointsAwarded] = @PointsAwarded,
                [UpdatedAt] = GETUTCDATE()
            WHERE [Id] = @Id;";

        var filteredPredictions = predictionsToUpdate
            .Where(p => p.PointsAwarded.HasValue)
            .Select(p => new { p.Id, p.PointsAwarded })
            .ToList();

        if (filteredPredictions.Any())
        {
            var command = new CommandDefinition(
                commandText: sql,
                parameters: filteredPredictions,
                cancellationToken: cancellationToken
            );

            await Connection.ExecuteAsync(command);
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Private Helper Methods

    private async Task<IEnumerable<League>> QueryAndMapLeagues(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(
            commandText: sql,
            parameters: param,
            cancellationToken: cancellationToken
        );

        var queryResult = await Connection.QueryAsync<League, LeagueMember?, (League League, LeagueMember? LeagueMember)>(
            command,
            (league, member) => (league, member),
            splitOn: "LeagueId"
        );

        var groupedLeagues = queryResult
            .GroupBy(x => x.League.Id)
            .Select(g =>
            {
                var firstLeague = g.First().League;
                var members = g.Select(x => x.LeagueMember).Where(m => m != null).ToList();

                return new League(
                    firstLeague.Id,
                    firstLeague.Name,
                    firstLeague.Price,
                    firstLeague.SeasonId,
                    firstLeague.AdministratorUserId,
                    firstLeague.EntryCode,
                    firstLeague.CreatedAt,
                    firstLeague.EntryDeadline,
                    members,
                    null
                );
            });

        return groupedLeagues;
    }

    #endregion
    
    #region Delete

    public async Task DeleteAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
        DELETE FROM [dbo].[LeagueMembers] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [dbo].[LeaguePrizeSetting] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [dbo].[Winnings] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [dbo].[Leagues] WHERE [Id] = @LeagueId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
