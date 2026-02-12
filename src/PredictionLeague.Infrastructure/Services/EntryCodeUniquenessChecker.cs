using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Domain.Services;
using System.Data;

namespace PredictionLeague.Infrastructure.Services;

public class EntryCodeUniquenessChecker : IEntryCodeUniquenessChecker
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public EntryCodeUniquenessChecker(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> IsCodeUnique(string entryCode, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT 
                COUNT(1) 
            FROM 
                [Leagues] 
            WHERE 
                [EntryCode] = @EntryCode;";
        
        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { EntryCode = entryCode },
            cancellationToken: cancellationToken
        );
        
        return await Connection.ExecuteScalarAsync<int>(command) == 0;
    }
}