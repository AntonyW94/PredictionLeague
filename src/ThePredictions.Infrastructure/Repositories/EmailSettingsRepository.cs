using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;

namespace ThePredictions.Infrastructure.Repositories;

public class EmailSettingsRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IEmailSettingsRepository
{
    public async Task<EmailSettings?> GetAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1
                [Id],
                [EmailsEnabled]
            FROM
                [EmailSettings]
            ORDER BY
                [Id];";

        var command = new CommandDefinition(sql, transaction: Transaction, cancellationToken: cancellationToken);

        return await Connection.QuerySingleOrDefaultAsync<EmailSettings>(command);
    }

    public async Task AddAsync(EmailSettings settings, CancellationToken cancellationToken)
    {
        const string sql = @"
            INSERT INTO [EmailSettings]
            (
                [EmailsEnabled]
            )
            VALUES
            (
                @EmailsEnabled
            );";

        var command = new CommandDefinition(sql, settings, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }

    public async Task UpdateAsync(EmailSettings settings, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE
                [EmailSettings]
            SET
                [EmailsEnabled] = @EmailsEnabled
            WHERE
                [Id] = @Id;";

        var command = new CommandDefinition(sql, settings, transaction: Transaction, cancellationToken: cancellationToken);

        await Connection.ExecuteAsync(command);
    }
}
