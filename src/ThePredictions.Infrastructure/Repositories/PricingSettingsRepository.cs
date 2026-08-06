using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class PricingSettingsRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IPricingSettingsRepository
{
    public async Task<PricingSettings?> GetAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1
                [Id],
                [BufferRate],
                [MinimumFloor]
            FROM
                [PricingSettings]
            ORDER BY
                [Id];";

        var command = new CommandDefinition(sql, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.QuerySingleOrDefaultAsync<PricingSettings>(command);
    }

    public async Task AddAsync(PricingSettings settings, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [PricingSettings]
            (
                [BufferRate],
                [MinimumFloor]
            )
            VALUES
            (
                @BufferRate,
                @MinimumFloor
            );";

        var command = new CommandDefinition(sql, settings, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateAsync(PricingSettings settings, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [PricingSettings]
            SET
                [BufferRate] = @BufferRate,
                [MinimumFloor] = @MinimumFloor
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(sql, settings, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}
