using System.Diagnostics.CodeAnalysis;
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

        var matches = await dbConnection.QueryAsync<MatchInRoundQueryResult>(sql, cancellationToken, new
            {
                request.RoundId,
                Scheduled = nameof(MatchStatus.Scheduled),
                InProgress = nameof(MatchStatus.InProgress),
                Completed = nameof(MatchStatus.Completed)
            });

        return matches.Select(m => new MatchInRoundDto(
            m.Id,
            m.MatchDateTimeUtc,
            m.MatchNumber,
            m.HomeTeamId,
            m.HomeTeamName,
            m.HomeTeamShortName,
            m.HomeTeamAbbreviation,
            m.HomeTeamLogoUrl,
            m.AwayTeamId,
            m.AwayTeamName,
            m.AwayTeamShortName,
            m.AwayTeamAbbreviation,
            m.AwayTeamLogoUrl,
            m.ActualHomeTeamScore,
            m.ActualAwayTeamScore,
            m.Status,
            m.PlaceholderHomeName,
            m.PlaceholderAwayName,
            m.CustomLockTimeUtc));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record MatchInRoundQueryResult(
        int Id,
        DateTime MatchDateTimeUtc,
        int? MatchNumber,
        int? HomeTeamId,
        string? HomeTeamName,
        string? HomeTeamShortName,
        string? HomeTeamAbbreviation,
        string? HomeTeamLogoUrl,
        int? AwayTeamId,
        string? AwayTeamName,
        string? AwayTeamShortName,
        string? AwayTeamAbbreviation,
        string? AwayTeamLogoUrl,
        int? ActualHomeTeamScore,
        int? ActualAwayTeamScore,
        MatchStatus Status,
        string? PlaceholderHomeName,
        string? PlaceholderAwayName,
        DateTime? CustomLockTimeUtc);
}