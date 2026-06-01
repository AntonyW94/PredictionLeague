using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class ServiceFeeRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IServiceFeeRepository
{
    public async Task<ServiceFee?> GetByProviderAsync(ServiceFeeProvider provider, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [Id],
                [Provider],
                [PercentFee],
                [FixedFee]
            FROM
                [ServiceFees]
            WHERE
                [Provider] = @Provider;";

        var command = new CommandDefinition(sql, new { Provider = provider.ToString() }, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.QuerySingleOrDefaultAsync<ServiceFee>(command);
    }

    public async Task AddAsync(ServiceFee serviceFee, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [ServiceFees]
            (
                [Provider],
                [PercentFee],
                [FixedFee]
            )
            VALUES
            (
                @Provider,
                @PercentFee,
                @FixedFee
            );";

        var command = new CommandDefinition(sql, ToParameters(serviceFee), transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateAsync(ServiceFee serviceFee, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [ServiceFees]
            SET
                [PercentFee] = @PercentFee,
                [FixedFee] = @FixedFee
            WHERE
                [Provider] = @Provider;";

        var command = new CommandDefinition(sql, ToParameters(serviceFee), transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    // Provider is stored as its enum name (matching the rest of the schema), so map explicitly.
    private static object ToParameters(ServiceFee serviceFee) => new
    {
        Provider = serviceFee.Provider.ToString(),
        serviceFee.PercentFee,
        serviceFee.FixedFee
    };
}
