using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchLeagueMembersQueryHandler : IRequestHandler<FetchLeagueMembersQuery, LeagueMembersPageDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public FetchLeagueMembersQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<LeagueMembersPageDto?> Handle(FetchLeagueMembersQuery request, CancellationToken cancellationToken)
    {
        // Verify user is the league administrator
        const string adminCheckSql = @"
            SELECT COUNT(*)
            FROM [Leagues]
            WHERE [Id] = @LeagueId
              AND [AdministratorUserId] = @CurrentUserId;";

        var isAdmin = await _dbConnection.QuerySingleOrDefaultAsync<int>(
            adminCheckSql,
            cancellationToken,
            new { request.LeagueId, request.CurrentUserId });

        if (isAdmin == 0)
            throw new UnauthorizedAccessException("Only the league administrator can view the members list.");

        const string sql = @"
            SELECT
                l.[Name] AS LeagueName,
                lm.[UserId],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS FullName,
                lm.[JoinedAtUtc],
                lm.[Status]
            FROM 
                [Leagues] l
            JOIN 
                [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
            JOIN 
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            WHERE 
                l.[Id] = @LeagueId
            ORDER BY 
                FullName;";
        
        var queryResult = await _dbConnection.QueryAsync<MemberQueryResult>(
            sql,
            cancellationToken,
            new { request.LeagueId, request.CurrentUserId, Pending = nameof(LeagueMemberStatus.Pending) }
        );
        
        var members = queryResult.ToList();
        if (members.Any())
        {
            return new LeagueMembersPageDto
            {
                LeagueName = members.First().LeagueName,
                Members = members.Select(m => new LeagueMemberDto
                (
                    m.UserId,
                    m.FullName,
                    m.JoinedAtUtc,
                    m.Status
                )).ToList()
            };
        }

        const string leagueNameSql = "SELECT [Name] FROM [Leagues] WHERE [Id] = @LeagueId;";
        var leagueName = await _dbConnection.QuerySingleOrDefaultAsync<string>(leagueNameSql, cancellationToken, new { request.LeagueId });

        return leagueName == null ? null : new LeagueMembersPageDto { LeagueName = leagueName };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record MemberQueryResult(
        string LeagueName,
        string UserId,
        string FullName,
        DateTime JoinedAtUtc,
        LeagueMemberStatus Status
    );
}