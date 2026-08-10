using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
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

        const string sql = @"
                IF NOT EXISTS (SELECT 1 FROM [UserOnboardingSkips] WHERE [UserId] = @UserId AND [StepKey] = @StepKey)
                    INSERT INTO [UserOnboardingSkips] ([UserId], [StepKey], [SkippedAtUtc])
                    VALUES (@UserId, @StepKey, SYSUTCDATETIME());";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: keys.Select(key => new { UserId = userId, StepKey = key }),
            transaction: Transaction,
            cancellationToken: cancellationToken
        );

        await Connection.ExecuteAsync(command);
    }
}
