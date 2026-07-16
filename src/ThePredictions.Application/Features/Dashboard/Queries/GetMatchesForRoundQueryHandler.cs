using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Dashboard.Queries;

public class GetMatchesForRoundQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetMatchesForRoundQuery, IEnumerable<MatchInRoundDto>>
{
    public async Task<IEnumerable<MatchInRoundDto>> Handle(GetMatchesForRoundQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                m.[Id],
                m.[MatchDateTimeUtc],
                m.[MatchNumber],
                m.[HomeTeamId],
                ht.[Name] AS HomeTeamName,
                ht.[ShortName] AS HomeTeamShortName,
                ht.[Abbreviation] AS HomeTeamAbbreviation,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                m.[AwayTeamId],
                at.[Name] AS AwayTeamName,
                at.[ShortName] AS AwayTeamShortName,
                at.[Abbreviation] AS AwayTeamAbbreviation,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                m.[ActualHomeTeamScore],
                m.[ActualAwayTeamScore],
                m.[Status],
                m.[PlaceholderHomeName],
                m.[PlaceholderAwayName],
                m.[CustomLockTimeUtc]
            FROM
                [Matches] m
            LEFT JOIN
                [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN
                [Teams] at ON m.[AwayTeamId] = at.[Id]
            WHERE
                m.[RoundId] = @RoundId
                AND m.[Status] IN (@Scheduled, @InProgress, @Completed)
            ORDER BY
                m.[MatchDateTimeUtc];";

        return await dbConnection.QueryAsync<MatchInRoundDto>(sql, cancellationToken, new
            {
                request.RoundId,
                Scheduled = nameof(MatchStatus.Scheduled),
                InProgress = nameof(MatchStatus.InProgress),
                Completed = nameof(MatchStatus.Completed)
            });
    }
}