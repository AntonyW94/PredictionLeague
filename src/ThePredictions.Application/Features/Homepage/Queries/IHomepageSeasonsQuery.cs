namespace ThePredictions.Application.Features.Homepage.Queries;

/// <summary>
/// Reads every season with its leagues and their approved members, for the figures on the public homepage.
/// </summary>
/// <remarks>
/// The statement this replaces called <c>GETUTCDATE()</c> three times - to decide which seasons to show at all, which were
/// under way and which were still to come - and worked out each season's prize pot with
/// <c>SUM(Price * MemberCount + ISNULL(PrizeFundOverride, 0))</c>, which is <c>Domain.Services.PrizeFund</c> written out in SQL
/// for the third time.
/// </remarks>
public interface IHomepageSeasonsQuery
{
    Task<HomepageSeasonsData> ExecuteAsync(CancellationToken cancellationToken);
}
