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
        FROM [Leagues] l
        LEFT JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]";

    public LeagueRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Create

    public async Task<League> CreateAsync(League league, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [Leagues] 
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
            INSERT INTO [LeagueMembers] ([LeagueId], [UserId], [Status], [JoinedAt], [ApprovedAt])
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
        
        SELECT l.* FROM [Leagues] l
        WHERE l.[SeasonId] = @SeasonId;

        SELECT lm.* FROM [LeagueMembers] lm
        INNER JOIN [Leagues] l ON l.Id = lm.LeagueId
        WHERE l.[SeasonId] = @SeasonId;

        SELECT up.* FROM [UserPredictions] up
        INNER JOIN [LeagueMembers] lm ON up.UserId = lm.UserId
        INNER JOIN [Leagues] l ON l.Id = lm.LeagueId
        WHERE l.[SeasonId] = @SeasonId AND up.MatchId IN (SELECT Id FROM Matches WHERE RoundId IN (SELECT Id FROM Rounds WHERE SeasonId = @SeasonId));";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { SeasonId = seasonId, RoundId = roundId },
            cancellationToken: cancellationToken
        );

        await using var multi = await Connection.QueryMultipleAsync(command);

        var leagues = multi.Read<League>().AsList();
        var membersLookup = multi.Read<LeagueMember>().ToLookup(m => m.LeagueId);
        var predictionsLookup = multi.Read<UserPrediction>().ToLookup(p => p.UserId);
        
        var result = leagues.Select(league =>
        {
            var members = membersLookup[league.Id].Select(member =>
            {
                var predictions = predictionsLookup[member.UserId].ToList();
                return new LeagueMember(member.LeagueId, member.UserId, member.Status, member.JoinedAt, member.ApprovedAt, predictions);
            }).ToList();

            return new League(
                league.Id,
                league.Name,
                league.Price,
                league.SeasonId,
                league.AdministratorUserId,
                league.EntryCode,
                league.CreatedAt,
                league.EntryDeadline,
                members,
                null
            );
        });

        return result;
    }

    public async Task<League?> GetByIdWithAllDataAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT l.* FROM [Leagues] l
            WHERE l.[Id] = @Id;

            SELECT lm.* FROM [LeagueMembers] lm
            WHERE lm.[LeagueId] = @Id;

            SELECT up.* FROM [UserPredictions] up
            INNER JOIN [LeagueMembers] lm ON up.LeagueMemberId = lm.Id
            WHERE lm.[LeagueId] = @Id;

            SELECT lps.*
            FROM [LeaguePrizeSettings] lps
            WHERE lps.[LeagueId] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id },
            cancellationToken: cancellationToken
        );

        await using var multi = await Connection.QueryMultipleAsync(command);

        var league = (await multi.ReadAsync<League>()).FirstOrDefault();
        if (league == null)
            return null;
      
        var membersData = (await multi.ReadAsync<LeagueMember>()).ToList();
        var predictionsLookup = (await multi.ReadAsync<UserPrediction>()).ToLookup(p => p.UserId);
        var prizeSettings = (await multi.ReadAsync<LeaguePrizeSetting>()).ToList();

        var hydratedMembers = membersData.Select(member =>
        {
            var memberPredictions = predictionsLookup[member.UserId].ToList();

            return new LeagueMember(
                member.LeagueId,
                member.UserId,
                member.Status,
                member.JoinedAt,
                member.ApprovedAt,
                memberPredictions
            );
        }).ToList();
       
        return new League(
            league.Id,
            league.Name,
            league.Price,
            league.SeasonId,
            league.AdministratorUserId,
            league.EntryCode,
            league.CreatedAt,
            league.EntryDeadline,
            hydratedMembers,
            prizeSettings
        );
    }

    public async Task<IEnumerable<League>> GetLeaguesByAdministratorIdAsync(string administratorId, CancellationToken cancellationToken)
    {
        const string sql = @"
        SELECT
            l.*, 
            lm.*
        FROM 
            [Leagues] l
        LEFT JOIN 
            [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
        WHERE
            l.[AdministratorUserId] = @AdministratorId;";

        return await QueryAndMapLeagues(sql, cancellationToken, new { AdministratorId = administratorId });
    }
    
    #endregion

    #region Update
    public async Task UpdateAsync(League league, CancellationToken cancellationToken)
    {
        const string updateLeagueSql = @"
            UPDATE [Leagues]
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
      
        const string deletePrizesSql = "DELETE FROM [LeaguePrizeSettings] WHERE [LeagueId] = @LeagueId;";
        
        var deletePrizesCommand = new CommandDefinition(
            deletePrizesSql,
            new { LeagueId = league.Id },
            cancellationToken: cancellationToken);
      
        await Connection.ExecuteAsync(deletePrizesCommand);

        if (league.PrizeSettings.Any())
        {
            const string insertPrizeSql = @"
            INSERT INTO [LeaguePrizeSettings] 
            (
                [LeagueId], [PrizeType], [Rank], [PrizeAmount], [PrizeDescription]
            ) 
            VALUES 
            (
                @LeagueId, @PrizeType, @Rank, @PrizeAmount, @PrizeDescription
            );";

            var insertPrizesCommand = new CommandDefinition(
                insertPrizeSql,
                league.PrizeSettings,
                cancellationToken: cancellationToken);
            await Connection.ExecuteAsync(insertPrizesCommand);
        }
        
        const string deleteMembersSql = "DELETE FROM [LeagueMembers] WHERE [LeagueId] = @LeagueId;";

        var deleteCommand = new CommandDefinition(
            deleteMembersSql,
            new { LeagueId = league.Id },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(deleteCommand);

        if (league.Members.Any())
        {
            const string insertMemberSql = @"
                INSERT INTO [LeagueMembers] ([LeagueId], [UserId], [Status], [JoinedAt], [ApprovedAt])
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
            UPDATE [LeagueMembers]
            SET [Status] = @Status,
                [ApprovedAt] = CASE WHEN @Status = 'Approved' THEN GETDATE() ELSE [ApprovedAt] END
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
            UPDATE [UserPredictions]
            SET [PointsAwarded] = @PointsAwarded,
                [UpdatedAt] = GETDATE()
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
        DELETE FROM [LeagueMembers] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [LeaguePrizeSettings] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [Winnings] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [Leagues] WHERE [Id] = @LeagueId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
