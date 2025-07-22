using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class GetRoundByIdQueryHandler : IRequestHandler<GetRoundByIdQuery, RoundDetailsDto?>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetRoundByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<RoundDetailsDto?> Handle(GetRoundByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[Id],
                r.[SeasonId],
                r.[RoundNumber],
                r.[StartDate],
                r.[Deadline],
                m.[Id],
                m.[MatchDateTime],
                m.[HomeTeamId],
                ht.[Name] AS HomeTeamName,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                m.[AwayTeamId],
                at.[Name] AS AwayTeamName,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                m.[ActualHomeTeamScore],
                m.[ActualAwayTeamScore],
                m.[Status]
            FROM [dbo].[Rounds] r
            LEFT JOIN [dbo].[Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [dbo].[Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [dbo].[Teams] at ON m.[AwayTeamId] = at.[Id]
            WHERE r.[Id] = @Id;";

        RoundDetailsDto? roundDetails = null;

        await _dbConnection.QueryAsync<RoundDto, MatchInRoundDto?, bool>(
            sql,
            cancellationToken,
            param: new { request.Id },
            map: (round, match) =>
            {
                if (roundDetails == null)
                    roundDetails = new RoundDetailsDto { Round = round };

                if (match != null)
                    roundDetails.Matches.Add(match);

                return true;
            },
            splitOn: "Id"
        );

        return roundDetails;
    }
}