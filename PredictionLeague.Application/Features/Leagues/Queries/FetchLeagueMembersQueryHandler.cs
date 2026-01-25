using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchLeagueMembersQueryHandler : IRequestHandler<FetchLeagueMembersQuery, LeagueMembersPageDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;
    private readonly ILeagueMembershipService _membershipService;

    public FetchLeagueMembersQueryHandler(
        IApplicationReadDbConnection dbConnection,
        ILeagueMembershipService membershipService)
    {
        _dbConnection = dbConnection;
        _membershipService = membershipService;
    }

    public async Task<LeagueMembersPageDto?> Handle(FetchLeagueMembersQuery request, CancellationToken cancellationToken)
    {
        await _membershipService.EnsureLeagueAdministratorAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

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