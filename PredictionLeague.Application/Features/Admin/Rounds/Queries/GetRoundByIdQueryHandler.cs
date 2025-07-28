using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Admin.Rounds;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

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
                r.[Id] AS RoundId,
                r.[SeasonId],
                r.[RoundNumber],
                r.[StartDate],
                r.[Deadline],
                m.[Id] AS MatchId,
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
            FROM [Rounds] r
            LEFT JOIN [Matches] m ON r.[Id] = m.[RoundId]
            LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            WHERE r.[Id] = @Id;";

        var queryResult = await _dbConnection.QueryAsync<RoundQueryResult>(sql, cancellationToken, new { request.Id });

        var results = queryResult.ToList();
        if (!results.Any())
        {
            return null;
        }

        var firstRow = results.First();
        var roundDto = new RoundDto(
            firstRow.RoundId,
            firstRow.SeasonId,
            firstRow.RoundNumber,
            firstRow.StartDate,
            firstRow.Deadline,
            results.Count(r => r.MatchId.HasValue)
        );

        var roundDetails = new RoundDetailsDto
        {
            Round = roundDto,
            Matches = results
                .Where(r => r.MatchId.HasValue)
                .Select(r => new MatchInRoundDto(
                    r.MatchId!.Value,
                    r.MatchDateTime!.Value,
                    r.HomeTeamId!.Value,
                    r.HomeTeamName!,
                    r.HomeTeamLogoUrl,
                    r.AwayTeamId!.Value,
                    r.AwayTeamName!,
                    r.AwayTeamLogoUrl,
                    r.ActualHomeTeamScore,
                    r.ActualAwayTeamScore,
                    Enum.Parse<MatchStatus>(r.Status!)
                )).ToList()
        };

        return roundDetails;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record RoundQueryResult(
        int RoundId,
        int SeasonId,
        int RoundNumber,
        DateTime StartDate,
        DateTime Deadline,
        int? MatchId,
        DateTime? MatchDateTime,
        int? HomeTeamId,
        string? HomeTeamName,
        string? HomeTeamLogoUrl,
        int? AwayTeamId,
        string? AwayTeamName,
        string? AwayTeamLogoUrl,
        int? ActualHomeTeamScore,
        int? ActualAwayTeamScore,
        string? Status
    );
}