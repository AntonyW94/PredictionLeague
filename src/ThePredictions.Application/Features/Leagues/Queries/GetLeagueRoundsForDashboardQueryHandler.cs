using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeagueRoundsForDashboardQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueRoundsForDashboardQuery, IEnumerable<RoundDto>>
{
    public async Task<IEnumerable<RoundDto>> Handle(GetLeagueRoundsForDashboardQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        const string sql = @"
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[ApiRoundName],
                r.[StartDateUtc],
                r.[DeadlineUtc],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] m WHERE m.[RoundId] = r.[Id]) as MatchCount
            FROM
                [Rounds] r
            JOIN
                [Leagues] l ON r.SeasonId = l.SeasonId
            WHERE
                l.[Id] = @LeagueId
                AND r.[Status] IN (@PublishedStatus, @CompletedStatus)
            ORDER BY
                r.[RoundNumber] DESC;";

        var parameters = new
        {
            request.LeagueId,
            PublishedStatus = nameof(RoundStatus.Published),
            CompletedStatus = nameof(RoundStatus.Completed)
        };

        var rounds = await dbConnection.QueryAsync<RoundQueryResult>(sql, cancellationToken, parameters);

        return rounds.Select(r => new RoundDto(
            r.Id,
            r.SeasonId,
            r.RoundNumber,
            r.ApiRoundName,
            r.StartDateUtc,
            r.DeadlineUtc,
            r.Status,
            r.MatchCount));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RoundQueryResult(
        int Id,
        int SeasonId,
        int RoundNumber,
        string? ApiRoundName,
        DateTime StartDateUtc,
        DateTime DeadlineUtc,
        RoundStatus Status,
        int MatchCount);
}