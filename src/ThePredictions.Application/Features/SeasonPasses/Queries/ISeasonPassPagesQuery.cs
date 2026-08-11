namespace ThePredictions.Application.Features.SeasonPasses.Queries;

/// <summary>
/// Reads everything the season-pass pages are built from: the seasons, the leagues' entry deadlines, how many players are
/// taking part in each season, and the passes this player already holds.
/// </summary>
/// <remarks>
/// One read behind four screens, which had four statements between them repeating the same building blocks - is a pass
/// already held, is entry still open, is this player eligible for a trial, how many are taking part. The available-passes
/// and past-passes pages were exact complements of each other: same conditions, one asking for entry still open and one
/// for entry closed everywhere. Nothing said so.
///
/// Three of those statements called <c>GETUTCDATE()</c>, so the answer depended on the database's clock rather than the
/// injected one, and two pages rendered at the same moment could disagree.
/// </remarks>
public interface ISeasonPassPagesQuery
{
    Task<SeasonPassPagesData> ExecuteAsync(string userId, CancellationToken cancellationToken);
}
