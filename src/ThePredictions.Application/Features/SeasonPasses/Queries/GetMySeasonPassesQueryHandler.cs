using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public class GetMySeasonPassesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetMySeasonPassesQuery, IEnumerable<MySeasonPassDto>>
{
    public async Task<IEnumerable<MySeasonPassDto>> Handle(GetMySeasonPassesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sp.[SeasonId],
                s.[Name] AS SeasonName,
                c.[LogoUrl] AS CompetitionLogoUrl,
                sp.[Tier],
                sp.[Source],
                sp.[AmountPaid],
                CAST(CASE WHEN sp.[Tier] = @PremiumTier THEN 1 ELSE 0 END AS BIT) AS HasSmsReminders,
                sp.[CreatedAtUtc]
            FROM
                [SeasonPasses] sp
            JOIN
                [Seasons] s ON s.[Id] = sp.[SeasonId]
            JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId]
            WHERE
                sp.[UserId] = @UserId
            ORDER BY
                sp.[CreatedAtUtc] DESC;";

        var passes = await dbConnection.QueryAsync<MySeasonPassQueryResult>(sql, cancellationToken, new { request.UserId, PremiumTier = nameof(SeasonPassTier.Premium) });

        return passes.Select(p => new MySeasonPassDto(
            p.SeasonId,
            p.SeasonName,
            p.CompetitionLogoUrl,
            p.Tier,
            p.Source,
            p.AmountPaid,
            p.HasSmsReminders,
            p.CreatedAtUtc));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record MySeasonPassQueryResult(
        int SeasonId,
        string SeasonName,
        string? CompetitionLogoUrl,
        string Tier,
        string Source,
        decimal AmountPaid,
        bool HasSmsReminders,
        DateTime CreatedAtUtc);
}
