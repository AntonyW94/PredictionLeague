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
        const string sql = @"
            SELECT
                l.[Name] AS LeagueName,
                lm.[UserId],
                u.[FirstName] + ' ' + u.[LastName] AS FullName,
                lm.[JoinedAt],
                lm.[Status],
                CAST(CASE WHEN lm.[Status] = @Pending AND l.[AdministratorUserId] = @CurrentUserId THEN 1 ELSE 0 END AS bit) AS CanBeApproved
            
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
                    m.JoinedAt,
                    m.Status,
                    m.CanBeApproved
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
        DateTime JoinedAt,
        LeagueMemberStatus Status,
        bool CanBeApproved
    );
}