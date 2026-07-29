using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using System.Data;

namespace ThePredictions.Infrastructure.Repositories;

public class LeagueRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext, IDateTimeProvider dateTimeProvider)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueRepository
{
    private const string GetLeaguesWithMembersSql = @"
        SELECT
            l.*,
            lm.*
        FROM [Leagues] l
        LEFT JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]";

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
                [PrizeFundOverride],
                [RequiresMemberApproval],
                [IsListed],
                [BankAccountName],
                [BankSortCode],
                [BankAccountNumber],
                [PaymentReferenceTemplate]
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
                @PrizeFundOverride,
                @RequiresMemberApproval,
                @IsListed,
                @BankAccountName,
                @BankSortCode,
                @BankAccountNumber,
                @PaymentReferenceTemplate
            );
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: league,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        var newLeagueId = await Connection.ExecuteScalarAsync<int>(command);

        var adminMember = LeagueMember.Create(newLeagueId, league.AdministratorUserId, dateTimeProvider);
        adminMember.Approve(dateTimeProvider);

        await AddMemberAsync(adminMember, cancellationToken);

        if (league.PrizeScheme is not null)
            await PersistPrizeSchemeAsync(newLeagueId, league.PrizeScheme, cancellationToken);

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
            prizeSettings: null,
            bankAccountName: league.BankAccountName,
            bankSortCode: league.BankSortCode,
            bankAccountNumber: league.BankAccountNumber,
            paymentReferenceTemplate: league.PaymentReferenceTemplate,
            prizeScheme: league.PrizeScheme,
            requiresMemberApproval: league.RequiresMemberApproval,
            isListed: league.IsListed
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
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Read

    public async Task<League?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = $"{GetLeaguesWithMembersSql} WHERE l.[Id] = @Id;";

        var league = (await QueryAndMapLeaguesAsync(sql, cancellationToken, new { Id = id })).FirstOrDefault();
        if (league is null)
            return null;

        var scheme = await LoadPrizeSchemeAsync(id, cancellationToken);
        return scheme is null ? league : WithPrizeScheme(league, scheme);
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
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await using var multi = await Connection.QueryMultipleAsync(command);

        var league = (await multi.ReadAsync<League>()).FirstOrDefault();
        if (league == null)
            return null;

        var membersData = (await multi.ReadAsync<LeagueMember>()).ToList();
        var prizeSettings = (await multi.ReadAsync<LeaguePrizeSetting>()).ToList();
        var roundResultsLookup = (await multi.ReadAsync<LeagueRoundResult>()).ToLookup(p => p.UserId);

        var prizeScheme = await LoadPrizeSchemeAsync(id, cancellationToken);

        var hydratedMembers = membersData.Select(member =>
        {
            var memberRoundResults = roundResultsLookup[member.UserId].ToList();

            return new LeagueMember(
                member.LeagueId,
                member.UserId,
                member.Status,
                member.IsAlertDismissed,
                member.IsArchivedByUser,
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
            prizeSettings,
            league.BankAccountName,
            league.BankSortCode,
            league.BankAccountNumber,
            league.PaymentReferenceTemplate,
            prizeScheme,
            league.RequiresMemberApproval,
            league.IsListed
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

        return await Connection.QueryAsync<LeagueRoundResult>(new CommandDefinition(sql, new { RoundId = roundId }, transaction: Transaction, cancellationToken: cancellationToken));
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

        return await Connection.QueryAsync<int>(new CommandDefinition(sql, new { SeasonId = seasonId }, transaction: Transaction, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<int>> GetLeagueIdsDueForPrizeFreezeAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id]
            FROM
                [Leagues] l
            JOIN
                [LeaguePrizeScheme] lpsc ON lpsc.[LeagueId] = l.[Id]
            WHERE
                l.[EntryDeadlineUtc] <= @NowUtc
                AND NOT EXISTS
                (
                    SELECT
                        1
                    FROM
                        [LeaguePrizeSettings] lps
                    WHERE
                        lps.[LeagueId] = l.[Id]
                );";

        return await Connection.QueryAsync<int>(new CommandDefinition(sql, new { NowUtc = nowUtc }, transaction: Transaction, cancellationToken: cancellationToken));
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
                [PrizeFundOverride] = @PrizeFundOverride,
                [RequiresMemberApproval] = @RequiresMemberApproval,
                [IsListed] = @IsListed,
                [BankAccountName] = @BankAccountName,
                [BankSortCode] = @BankSortCode,
                [BankAccountNumber] = @BankAccountNumber,
                [PaymentReferenceTemplate] = @PaymentReferenceTemplate
            WHERE
                [Id] = @Id;";

        var leagueCommand = new CommandDefinition(
            updateLeagueSql,
            league,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(leagueCommand);

        const string deletePrizesSql = "DELETE FROM [LeaguePrizeSettings] WHERE [LeagueId] = @LeagueId;";

        var deletePrizesCommand = new CommandDefinition(
            deletePrizesSql,
            new { LeagueId = league.Id },
            transaction: Transaction,
            cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(deletePrizesCommand);

        if (league.PrizeSettings.Any())
        {
            const string insertPrizeSql = @"
            INSERT INTO [LeaguePrizeSettings]
            (
                [LeagueId], [PrizeType], [Rank], [PrizeAmount], [PrizeDescription], [Stage]
            )
            VALUES
            (
                @LeagueId, @PrizeType, @Rank, @PrizeAmount, @PrizeDescription, @Stage
            );";

            var insertPrizesCommand = new CommandDefinition(
                insertPrizeSql,
                league.PrizeSettings,
                transaction: Transaction,
                cancellationToken: cancellationToken);
            await Connection.ExecuteAsync(insertPrizesCommand);
        }

        const string deleteMembersSql = "DELETE FROM [LeagueMembers] WHERE [LeagueId] = @LeagueId;";

        var deleteCommand = new CommandDefinition(
            deleteMembersSql,
            new { LeagueId = league.Id },
            transaction: Transaction,
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
            }), transaction: Transaction, cancellationToken: cancellationToken);

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
            transaction: Transaction,
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
                transaction: Transaction,
                cancellationToken: cancellationToken
            ));
    }

    #endregion

    public Task SavePrizeSchemeAsync(int leagueId, LeaguePrizeScheme scheme, CancellationToken cancellationToken) =>
        PersistPrizeSchemeAsync(leagueId, scheme, cancellationToken);

    #region Private Helper Methods

    private async Task PersistPrizeSchemeAsync(int leagueId, LeaguePrizeScheme scheme, CancellationToken cancellationToken)
    {
        const string deleteSchemeSql = "DELETE FROM [LeaguePrizeScheme] WHERE [LeagueId] = @LeagueId;";

        await Connection.ExecuteAsync(new CommandDefinition(
            deleteSchemeSql,
            new { LeagueId = leagueId },
            transaction: Transaction,
            cancellationToken: cancellationToken));

        const string insertSchemeSql = @"
            INSERT INTO [LeaguePrizeScheme]
            (
                [LeagueId],
                [SetAtUtc],
                [SetByUserId]
            )
            VALUES
            (
                @LeagueId,
                @SetAtUtc,
                @SetByUserId
            );
            SELECT CAST(SCOPE_IDENTITY() as int);";

        var schemeId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            insertSchemeSql,
            new
            {
                LeagueId = leagueId,
                scheme.SetAtUtc,
                scheme.SetByUserId
            },
            transaction: Transaction,
            cancellationToken: cancellationToken));

        if (!scheme.Entries.Any())
            return;

        const string insertEntrySql = @"
            INSERT INTO [LeaguePrizeSchemeEntries]
            (
                [LeaguePrizeSchemeId],
                [Category],
                [PerEntryPounds],
                [RankTableJson]
            )
            VALUES
            (
                @LeaguePrizeSchemeId,
                @Category,
                @PerEntryPounds,
                @RankTableJson
            );";

        var entryRows = scheme.Entries.Select(e => new
        {
            LeaguePrizeSchemeId = schemeId,
            Category = e.Category.ToString(),
            e.PerEntryPounds,
            e.RankTableJson
        });

        await Connection.ExecuteAsync(new CommandDefinition(
            insertEntrySql,
            entryRows,
            transaction: Transaction,
            cancellationToken: cancellationToken));

        // Keep the league's HasPrizes flag in step (a scheme means it awards prizes whenever the
        // pot is non-zero: a paid entry fee or admin top-up money). Done here rather than via
        // UpdateAsync, which would rewrite members/prize settings. Derived from the league's own
        // columns so it's correct regardless of call order.
        const string updateHasPrizesSql = @"
            UPDATE [Leagues]
            SET [HasPrizes] = CASE WHEN [Price] > 0 OR ISNULL([PrizeFundOverride], 0) > 0 THEN 1 ELSE 0 END
            WHERE [Id] = @LeagueId;";

        await Connection.ExecuteAsync(new CommandDefinition(
            updateHasPrizesSql,
            new { LeagueId = leagueId },
            transaction: Transaction,
            cancellationToken: cancellationToken));
    }

    private async Task<LeaguePrizeScheme?> LoadPrizeSchemeAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT lps.* FROM [LeaguePrizeScheme] lps
            WHERE lps.[LeagueId] = @LeagueId;

            SELECT lpse.* FROM [LeaguePrizeSchemeEntries] lpse
            INNER JOIN [LeaguePrizeScheme] lps ON lps.[Id] = lpse.[LeaguePrizeSchemeId]
            WHERE lps.[LeagueId] = @LeagueId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId },
            transaction: Transaction,
            cancellationToken: cancellationToken);

        await using var multi = await Connection.QueryMultipleAsync(command);

        var header = (await multi.ReadAsync<LeaguePrizeScheme>()).FirstOrDefault();
        if (header is null)
            return null;

        var entries = (await multi.ReadAsync<LeaguePrizeSchemeEntry>()).ToList();

        return new LeaguePrizeScheme(
            header.Id,
            header.LeagueId,
            header.SetAtUtc,
            header.SetByUserId,
            entries);
    }

    private static League WithPrizeScheme(League league, LeaguePrizeScheme scheme) =>
        new(
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
            league.Members,
            league.PrizeSettings,
            league.BankAccountName,
            league.BankSortCode,
            league.BankAccountNumber,
            league.PaymentReferenceTemplate,
            scheme,
            league.RequiresMemberApproval,
            league.IsListed);

    private async Task<IEnumerable<League>> QueryAndMapLeaguesAsync(string sql, CancellationToken cancellationToken, object? param = null)
    {
        var command = new CommandDefinition(
            commandText: sql,
            parameters: param,
            transaction: Transaction,
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
                    null,
                    firstLeague.BankAccountName,
                    firstLeague.BankSortCode,
                    firstLeague.BankAccountNumber,
                    firstLeague.PaymentReferenceTemplate,
                    requiresMemberApproval: firstLeague.RequiresMemberApproval,
                    isListed: firstLeague.IsListed
                );
            });

        return groupedLeagues;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(int leagueId, CancellationToken cancellationToken)
    {
        // [LeagueMemberStats] has a foreign key to [Leagues] with no cascade, so its rows have to go
        // first or the delete fails. Every approved member now has a row from the moment they join,
        // rather than only once a round has gone live, so this is not a rare path.
        const string sql = @"
        DELETE FROM [LeagueMemberStats] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [LeagueMembers] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [LeaguePrizeSettings] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [LeaguePrizeScheme] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [Winnings] WHERE [LeagueId] = @LeagueId;
        DELETE FROM [Leagues] WHERE [Id] = @LeagueId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { LeagueId = leagueId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
