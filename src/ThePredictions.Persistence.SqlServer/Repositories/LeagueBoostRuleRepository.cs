using System.Diagnostics.CodeAnalysis;
using Dapper;
using ThePredictions.Application.Data;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Persistence.SqlServer.Repositories;

[ExcludeFromCodeCoverage(Justification = "Repository: a thin Dapper wrapper over SQL. A unit test would assert only that a mocked connection received a string; correctness lives in the SQL.")]
public class LeagueBoostRuleRepository(IDbConnectionFactory connectionFactory, IDbTransactionContext transactionContext)
    : RepositoryBase(connectionFactory, transactionContext), ILeagueBoostRuleRepository
{
    public async Task<bool> HasRulesAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM [LeagueBoostRules] WHERE [LeagueId] = @LeagueId) THEN 1 ELSE 0 END;";

        var command = new CommandDefinition(sql, new { LeagueId = leagueId }, transaction: Transaction, cancellationToken: cancellationToken);
        return await Connection.ExecuteScalarAsync<bool>(command);
    }

    public async Task SetRulesAsync(int leagueId, IReadOnlyList<LeagueBoostSelectionDto> selections, CancellationToken cancellationToken)
    {
        // Replace-all: clear existing rules (windows cascade) then insert the enabled selections.
        const string deleteSql = "DELETE FROM [LeagueBoostRules] WHERE [LeagueId] = @LeagueId;";
        await Connection.ExecuteAsync(new CommandDefinition(deleteSql, new { LeagueId = leagueId }, transaction: Transaction, cancellationToken: cancellationToken));

        const string boostIdSql = "SELECT [Id] FROM [BoostDefinitions] WHERE [Code] = @Code;";

        const string insertRuleSql = @"
            INSERT INTO [LeagueBoostRules] ([LeagueId], [BoostDefinitionId], [TotalUsesPerSeason], [IsEnabled])
            VALUES (@LeagueId, @BoostDefinitionId, @TotalUsesPerSeason, 1);
            SELECT CAST(SCOPE_IDENTITY() as int);";

        const string insertWindowSql = @"
            INSERT INTO [LeagueBoostWindows] ([LeagueBoostRuleId], [StartRoundNumber], [EndRoundNumber], [MaxUsesInWindow])
            VALUES (@LeagueBoostRuleId, @StartRoundNumber, @EndRoundNumber, @MaxUsesInWindow);";

        foreach (var selection in selections.Where(s => s.IsEnabled))
        {
            var boostId = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(boostIdSql, new { Code = selection.BoostCode }, transaction: Transaction, cancellationToken: cancellationToken));
            if (boostId is null)
                continue;

            var ruleId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
                insertRuleSql,
                new { LeagueId = leagueId, BoostDefinitionId = boostId.Value, selection.TotalUsesPerSeason },
                transaction: Transaction,
                cancellationToken: cancellationToken));

            if (selection.Windows.Count == 0)
                continue;

            var windowRows = selection.Windows.Select(w => new
            {
                LeagueBoostRuleId = ruleId,
                w.StartRoundNumber,
                w.EndRoundNumber,
                w.MaxUsesInWindow
            });

            await Connection.ExecuteAsync(new CommandDefinition(insertWindowSql, windowRows, transaction: Transaction, cancellationToken: cancellationToken));
        }
    }
}
