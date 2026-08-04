using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class GetSeasonPassHoldersQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetSeasonPassHoldersQuery, IEnumerable<SeasonPassHolderDto>>
{
    public async Task<IEnumerable<SeasonPassHolderDto>> Handle(GetSeasonPassHoldersQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[UserId],
                u.[FirstName] + ' ' + u.[LastName] AS FullName,
                u.[Email],
                sp.[Tier],
                sp.[Source],
                sp.[AmountPaid],
                sp.[SmsFeePaid],
                sp.[CreatedAtUtc]
            FROM
                [SeasonPasses] sp
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = sp.[UserId]
            WHERE
                sp.[SeasonId] = @SeasonId
            ORDER BY
                sp.[CreatedAtUtc];";

        var holders = await dbConnection.QueryAsync<SeasonPassHolderQueryResult>(sql, cancellationToken, new { request.SeasonId });

        return holders.Select(h => new SeasonPassHolderDto(
            h.UserId,
            h.FullName,
            h.Email,
            h.Tier,
            h.Source,
            h.AmountPaid,
            h.SmsFeePaid,
            h.CreatedAtUtc));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record SeasonPassHolderQueryResult(
        string UserId,
        string FullName,
        string Email,
        SeasonPassTier Tier,
        SeasonPassSource Source,
        decimal AmountPaid,
        decimal SmsFeePaid,
        DateTime CreatedAtUtc);
}
