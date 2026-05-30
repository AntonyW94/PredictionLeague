using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class CompetitionRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ICompetitionRepository
{
    #region Create

    public async Task<Competition> CreateAsync(Competition competition, CancellationToken cancellationToken)
    {
        const string sql = @"
                INSERT INTO [Competitions]
                (
                    [Code],
                    [Name],
                    [Type],
                    [LogoUrl],
                    [ApiLeagueId],
                    [CreatedAtUtc]
                )
                OUTPUT INSERTED.*
                VALUES
                (
                    @Code,
                    @Name,
                    @Type,
                    @LogoUrl,
                    @ApiLeagueId,
                    @CreatedAtUtc
                );";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: competition,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleAsync<Competition>(command);
    }

    #endregion

    #region Read

    public async Task<Competition?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT
                    c.[Id],
                    c.[Code],
                    c.[Name],
                    c.[Type],
                    c.[LogoUrl],
                    c.[ApiLeagueId],
                    c.[CreatedAtUtc]
                FROM [Competitions] c
                WHERE c.[Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Id = id },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleOrDefaultAsync<Competition>(command);
    }

    public async Task<Competition?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT
                    c.[Id],
                    c.[Code],
                    c.[Name],
                    c.[Type],
                    c.[LogoUrl],
                    c.[ApiLeagueId],
                    c.[CreatedAtUtc]
                FROM [Competitions] c
                WHERE c.[Code] = @Code;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { Code = code },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.QuerySingleOrDefaultAsync<Competition>(command);
    }

    public async Task<bool> HasSeasonsAsync(int competitionId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Seasons] s
                    WHERE s.[CompetitionId] = @CompetitionId
                ) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { CompetitionId = competitionId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    #endregion

    #region Update

    public async Task UpdateAsync(Competition competition, CancellationToken cancellationToken)
    {
        const string sql = @"
                UPDATE [Competitions]
                SET
                    [Code] = @Code,
                    [Name] = @Name,
                    [Type] = @Type,
                    [LogoUrl] = @LogoUrl,
                    [ApiLeagueId] = @ApiLeagueId
                WHERE [Id] = @Id;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: competition,
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(int competitionId, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM [Competitions] WHERE [Id] = @CompetitionId;";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { CompetitionId = competitionId },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }

    #endregion
}
