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