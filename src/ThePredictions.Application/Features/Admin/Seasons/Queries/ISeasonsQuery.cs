namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>
/// Reads every season for the administrator's season screens, with the rounds and fixtures the counts on those screens
/// are worked out from.
/// </summary>
/// <remarks>
/// One read serves the list and the single-season screen, which had a twenty-column statement each differing only in a
/// <c>WHERE</c> clause. There are three seasons in the database.
///
/// What is gone from those statements: four correlated counts that each hardcoded a round status as a text literal, a
/// nested <c>UNION</c> that counted the teams in a season, and the ordering. All of them are rules.
/// </remarks>
public interface ISeasonsQuery
{
    Task<SeasonsData> ExecuteAsync(CancellationToken cancellationToken);
}
