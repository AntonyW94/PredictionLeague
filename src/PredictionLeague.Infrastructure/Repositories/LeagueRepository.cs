using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Boosts;
using PredictionLeague.Domain.Common;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class LeagueRepository : ILeagueRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    private const string GetLeaguesWithMembersSql = @"
        SELECT
            l.*,
            lm.*
        FROM [Leagues] l
        LEFT JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]";

    public LeagueRepository(IDbConnectionFactory connectionFactory, IDateTimeProvider dateTimeProvider)
    {
        _connectionFactory = connectionFactory;
        _dateTimeProvider = dateTimeProvider;
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
                [CreatedAtUtc], 
                [EntryDeadlineUtc],
                [PointsForExactScore],
                [PointsForCorrectResult],
                [IsFree],
                [HasPrizes],
                [PrizeFundOverride]
            )
            VALUES 
            (
                @Name, 
                @SeasonId, 
                @Price, 
                @AdministratorUserId, 
                @EntryCode, 
                @CreatedAtUtc, 
                @EntryDeadlineUtc,
                @PointsForExactScore,
                @PointsForCorrectResult,
                @IsFree,
                @HasPrizes,
                @PrizeFundOverride
            );
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: league,
            cancellationToken: cancellationToken
        );

        var newLeagueId = await Connection.ExecuteScalarAsync<int>(command);

        var adminMember = LeagueMember.Create(newLeagueId, league.AdministratorUserId, _dateTimeProvider);
        adminMember.Approve(_dateTimeProvider);

        await AddMemberAsync(adminMember, cancellationToken);

        var newLeague = new League(
            id: newLeagueId,
            name: league.Name,
            seasonId: league.SeasonId,
            administratorUserId: league.AdministratorUserId,
            entryCode: league.EntryCode,
            createdAtUtc: league.CreatedAtUtc,
            entryDeadlineUtc: league.EntryDeadlineUtc,
            pointsForExactScore: league.PointsForExactScore,
            pointsForCorrectResult: league.PointsForCorrectResult,
            price: league.Price,
            isFree: league.IsFree,
            hasPrizes: league.HasPrizes,
            prizeFundOverride: league.PrizeFundOverride,
            members: [adminMember], 
            prizeSettings: null
        );

        return newLeague;
    }

    private async Task AddMemberAsync(LeagueMember member, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [LeagueMembers] ([LeagueId], [UserId], [Status], [JoinedAtUtc], [ApprovedAtUtc])
            VALUES (@LeagueId, @UserId, @Status, @JoinedAtUtc, @ApprovedAtUtc);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new
            {
                member.LeagueId,
                member.UserId,
                Status = member.Status.ToString(),
                member.JoinedAtUtc,
                member.ApprovedAtUtc
            },
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<League?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = $"{GetLeaguesWithMembersSql} WHERE l.[Id] = @Id;";

        return (await QueryAndMapLeaguesAsync(sql, cancellationToken, new { Id = id })).FirstOrDefault();
    }

    public async Task<League?> GetByEntryCodeAsync(string? entryCode, CancellationToken cancellationToken)
    {
        const string sql = $"{GetLeaguesWithMembersSql} WHERE l.[EntryCode] = @EntryCode;";

        return (await QueryAndMapLeaguesAsync(sql, cancellationToken, new { EntryCode = entryCode })).FirstOrDefault();
    }

    public async Task<League?> GetByIdWithAllDataAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT l.* FROM [Leagues] l
            WHERE l.[Id] = @Id;

            SELECT lm.* FROM [LeagueMembers] lm
            WHERE lm.[LeagueId] = @Id
            AND lm.[Status] = @ApprovedStatus;

            SELECT lps.*
            FROM [LeaguePrizeSettings] lps
            WHERE lps.[LeagueId] = @Id;

            SELECT lrr.*, rr.[ExactScoreCount]
            FROM [LeagueRoundResults] lrr
            INNER JOIN [RoundResults] rr ON rr.[RoundId] = lrr.[RoundId] AND rr.[UserId] = lrr.[UserId]
            WHERE lrr.[LeagueId] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id, ApprovedStatus = nameof(LeagueMemberStatus.Approved) },
            cancellationToken: cancellationToken
        );

        await using var multi = await Connection.QueryMultipleAsync(command);

        var league = (await multi.ReadAsync<League>()).FirstOrDefault();
        if (league == null)
            return null;

        var membersData = (await multi.ReadAsync<LeagueMember>()).ToList();
        var prizeSettings = (await multi.ReadAsync<LeaguePrizeSetting>()).ToList();
        var roundResultsLookup = (await multi.ReadAsync<LeagueRoundResult>()).ToLookup(p => p.UserId);

        var hydratedMembers = membersData.Select(member =>
        {
            var memberRoundResults = roundResultsLookup[member.UserId].ToList();

            return new LeagueMember(
                member.LeagueId,
                member.UserId,
                member.Status,
                member.IsAlertDismissed,
                member.JoinedAtUtc,
                member.ApprovedAtUtc,
                memberRoundResults
            );
        }).ToList();

        return new League(
            league.Id,
            league.Name,
            league.SeasonId,
            league.AdministratorUserId,
            league.EntryCode,
            league.CreatedAtUtc,
            league.EntryDeadlineUtc,
            league.PointsForExactScore,
            league.PointsForCorrectResult,
            league.Price,
            league.IsFree,
            league.HasPrizes,
            league.PrizeFundOverride,
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

        return await QueryAndMapLeaguesAsync(sql, cancellationToken, new { AdministratorId = administratorId });
    }

    public async Task<IEnumerable<LeagueRoundResult>> GetLeagueRoundResultsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT                
                [LeagueId],
                [RoundId],
                [UserId],
                [BasePoints],
                [BoostedPoints],
                [HasBoost],
                [AppliedBoostCode]
            FROM 
                [LeagueRoundResults]
            WHERE 
                [RoundId] = @RoundId;";

        return await Connection.QueryAsync<LeagueRoundResult>(new CommandDefinition(sql, new { RoundId = roundId }, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<int>> GetLeagueIdsForSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT 
                [Id] 
            FROM 
                [Leagues] 
            WHERE 
                [SeasonId] = @SeasonId
                AND [HasPrizes] = 1";

        return await Connection.QueryAsync<int>(new CommandDefinition(sql, new { SeasonId = seasonId }, cancellationToken: cancellationToken));
    }

    #endregion

    #region Update

    public async Task UpdateAsync(League league, CancellationToken cancellationToken)
    {
        const string updateLeagueSql = @"
            UPDATE
                [Leagues]
            SET
                [Name] = @Name,
                [Price] = @Price,
                [EntryCode] = @EntryCode,
                [EntryDeadlineUtc] = @EntryDeadlineUtc,
                [PointsForExactScore] = @PointsForExactScore,
                [PointsForCorrectResult] = @PointsForCorrectResult,
                [IsFree] = @IsFree,
                [HasPrizes] = @HasPrizes,
                [PrizeFundOverride] = @PrizeFundOverride
            WHERE
                [Id] = @Id;";

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
                INSERT INTO [LeagueMembers] ([LeagueId], [UserId], [Status], [JoinedAtUtc], [ApprovedAtUtc])
                VALUES (@LeagueId, @UserId, @Status, @JoinedAtUtc, @ApprovedAtUtc);";

            var insertCommand = new CommandDefinition(insertMemberSql, league.Members.Select(m => new
            {
                m.LeagueId,
                m.UserId,
                Status = m.Status.ToString(),
                m.JoinedAtUtc,
                m.ApprovedAtUtc
            }), cancellationToken: cancellationToken);

            await Connection.ExecuteAsync(insertCommand);
        }
    }

    public async Task UpdateLeagueRoundResultsAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
            MERGE [LeagueRoundResults] AS target
            USING (
                    SELECT
                        lm.[LeagueId],
                        rr.[RoundId],
                        rr.[UserId],
                        (
                            (rr.[ExactScoreCount] * l.[PointsForExactScore]) + 
                            (rr.[CorrectResultCount] * l.[PointsForCorrectResult])
                        ) AS [BasePoints]
                    FROM 
                        [RoundResults] rr
                    INNER JOIN 
                        [Rounds] r ON r.[Id] = rr.[RoundId]
                    INNER JOIN 
                        [Leagues] l ON l.[SeasonId] = r.[SeasonId]
                    INNER JOIN 
                        [LeagueMembers] lm ON lm.[LeagueId] = l.[Id] AND lm.[UserId]  = rr.[UserId] AND lm.[Status]  = @ApprovedStatus
                    WHERE 
                        rr.[RoundId] = @RoundId
                   ) AS src
            ON target.[LeagueId] = src.[LeagueId]
               AND target.[RoundId] = src.[RoundId]
               AND target.[UserId]  = src.[UserId]
            
            WHEN MATCHED THEN
                UPDATE SET 
                    target.[BasePoints]       = src.[BasePoints],
                    target.[BoostedPoints]    = src.[BasePoints],
                    target.[HasBoost]         = 0,
                    target.[AppliedBoostCode] = NULL
            
            WHEN NOT MATCHED BY TARGET THEN
                INSERT ([LeagueId], [RoundId], [UserId], [BasePoints], [BoostedPoints], [HasBoost], [AppliedBoostCode])
                VALUES (src.[LeagueId], src.[RoundId], src.[UserId], src.[BasePoints], src.[BasePoints], 0, NULL);";

        var command = new CommandDefinition(
            sql,
            new
            {
                RoundId = roundId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved)
            },
            cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateLeagueRoundBoostsAsync(IEnumerable<LeagueRoundBoostUpdate> updates, CancellationToken cancellationToken)
    {
        const string sql = @"
            MERGE [LeagueRoundResults] AS target
            USING (
                SELECT
                    @LeagueId          AS [LeagueId],
                    @RoundId           AS [RoundId],
                    @UserId            AS [UserId],
                    @BoostedPoints     AS [BoostedPoints],
                    @HasBoost          AS [HasBoost],
                    @AppliedBoostCode  AS [AppliedBoostCode]
            ) AS src
            ON target.[LeagueId] = src.[LeagueId]
               AND target.[RoundId] = src.[RoundId]
               AND target.[UserId]  = src.[UserId]
            WHEN MATCHED THEN
                UPDATE SET
                    target.[BoostedPoints]    = src.[BoostedPoints],
                    target.[HasBoost]         = src.[HasBoost],
                    target.[AppliedBoostCode] = src.[AppliedBoostCode];";

        await Connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                updates.Select(u => new
                {
                    u.LeagueId,
                    u.RoundId,
                    u.UserId,
                    u.BoostedPoints,
                    u.HasBoost,
                    u.AppliedBoostCode
                }),
                cancellationToken: cancellationToken
            ));
    }

    #endregion

    #region Private Helper Methods

    private async Task<IEnumerable<League>> QueryAndMapLeaguesAsync(string sql, CancellationToken cancellationToken, object? param = null)
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
                    firstLeague.SeasonId,
                    firstLeague.AdministratorUserId,
                    firstLeague.EntryCode,
                    firstLeague.CreatedAtUtc,
                    firstLeague.EntryDeadlineUtc,
                    firstLeague.PointsForExactScore,
                    firstLeague.PointsForCorrectResult,
                    firstLeague.Price,
                    firstLeague.IsFree,
                    firstLeague.HasPrizes,
                    firstLeague.PrizeFundOverride,
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
