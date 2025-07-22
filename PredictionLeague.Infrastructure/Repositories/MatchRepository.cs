using Dapper;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Models;
using System.Data;

namespace PredictionLeague.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection Connection => _connectionFactory.CreateConnection();

    public MatchRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region Read

    public async Task<IEnumerable<Match>> GetByRoundIdAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT
                    m.*,
                    ht.*,
                    at.*
                FROM [Matches] m
                INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
                INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
                WHERE m.[RoundId] = @RoundId
                ORDER BY m.[MatchDateTime];";

        var command = new CommandDefinition(
            commandText: sql,
            parameters: new { RoundId = roundId },
            cancellationToken: cancellationToken
        );

        var matches = await Connection.QueryAsync<Match, Team, Team, Match>(
            command,
            (match, homeTeam, awayTeam) =>
            {
                match.HomeTeam = homeTeam;
                match.AwayTeam = awayTeam;
                return match;
            },
            splitOn: "Id,Id"
        );
        return matches;
    }

    #endregion
}