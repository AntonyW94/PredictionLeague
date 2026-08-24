using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class OnboardingSkipRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), IOnboardingSkipRepository
{
    public async Task AddSkipsAsync(string userId, IEnumerable<string> stepKeys, CancellationToken cancellationToken)
    {
        var keys = stepKeys.Distinct().ToList();
        if (keys.Count == 0)
            return;

        // One statement, so the "is it already skipped?" test becomes a join over the whole batch rather than a
        // lookup per key. Still idempotent: a key already skipped is filtered out rather than inserted again.
        const string sql = @"
            INSERT INTO [UserOnboardingSkips]
            (
                [UserId],
                [StepKey],
                [SkippedAtUtc]
            )
            SELECT
                @UserId,
                src.[StepKey],
                SYSUTCDATETIME()
            FROM
                OPENJSON(@Rows)
                WITH (
                    [StepKey] nvarchar(100) 'strict $.StepKey'
                ) src
            LEFT JOIN
                [UserOnboardingSkips] existing ON existing.[UserId] = @UserId
                    AND existing.[StepKey] = src.[StepKey]
            WHERE
                existing.[StepKey] IS NULL;";

        var rows = keys
            .Select(key => new { StepKey = key })
            .ToList();

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { UserId = userId, Rows = JsonRows.From(rows) },
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }
}
