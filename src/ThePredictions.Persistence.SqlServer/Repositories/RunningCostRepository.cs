using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class RunningCostRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IRunningCostRepository
{
    public async Task<RunningCost?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [Id],
                [Name],
                [Amount],
                [Frequency],
                [StartDateUtc],
                [EndDateUtc],
                [Notes],
                [CreatedAtUtc]
            FROM
                [RunningCosts]
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(sql, new { Id = id }, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.QuerySingleOrDefaultAsync<RunningCost>(command);
    }

    public async Task AddAsync(RunningCost runningCost, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [RunningCosts]
            (
                [Name],
                [Amount],
                [Frequency],
                [StartDateUtc],
                [EndDateUtc],
                [Notes],
                [CreatedAtUtc]
            )
            VALUES
            (
                @Name,
                @Amount,
                @Frequency,
                @StartDateUtc,
                @EndDateUtc,
                @Notes,
                @CreatedAtUtc
            );";

        var command = new CommandDefinition(sql, ToParameters(runningCost), transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateAsync(RunningCost runningCost, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [RunningCosts]
            SET
                [Name] = @Name,
                [Amount] = @Amount,
                [Frequency] = @Frequency,
                [StartDateUtc] = @StartDateUtc,
                [EndDateUtc] = @EndDateUtc,
                [Notes] = @Notes
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(sql, ToParameters(runningCost), transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM [RunningCosts] WHERE [Id] = @Id;";

        var command = new CommandDefinition(sql, new { Id = id }, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    // Enum columns are stored as their names (matching the rest of the schema), so map explicitly.
    private static object ToParameters(RunningCost runningCost) => new
    {
        runningCost.Id,
        runningCost.Name,
        runningCost.Amount,
        Frequency = runningCost.Frequency.ToString(),
        runningCost.StartDateUtc,
        runningCost.EndDateUtc,
        runningCost.Notes,
        runningCost.CreatedAtUtc
    };
}
