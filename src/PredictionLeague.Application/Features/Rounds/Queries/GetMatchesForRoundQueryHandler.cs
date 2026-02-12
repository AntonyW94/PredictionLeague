using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Rounds.Queries;

public class GetMatchesForRoundQueryHandler : IRequestHandler<GetMatchesForRoundQuery, IEnumerable<MatchInRoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMatchesForRoundQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<MatchInRoundDto>> Handle(GetMatchesForRoundQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                m.[Id],
                m.[MatchDateTimeUtc],
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
                m.[Status]
            FROM 
                [Matches] m
            JOIN 
                [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            JOIN 
                [Teams] at ON m.[AwayTeamId] = at.[Id]
            WHERE 
                m.[RoundId] = @RoundId
            ORDER BY 
                m.[MatchDateTimeUtc];";

        return await _dbConnection.QueryAsync<MatchInRoundDto>(sql, cancellationToken, new { request.RoundId });
    }
}