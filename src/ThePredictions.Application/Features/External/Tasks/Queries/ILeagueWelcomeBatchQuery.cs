namespace ThePredictions.Application.Features.External.Tasks.Queries;

/// <summary>
/// Reads the leagues whose entry closed inside the window, everyone approved in them, and what those leagues offer.
/// </summary>
/// <remarks>
/// The window itself stays in the read - it is choosing which rows, and both instants come from the caller's clock. What does not
/// stay is who gets the email: the <c>NOT EXISTS</c> against the sent-log, and a <c>NOT EXISTS</c> nested inside another one that
/// skipped a league whose prizes were half-configured. Those decide whether a real message reaches a real player, and they were
/// four levels deep in a statement nothing could test.
/// </remarks>
public interface ILeagueWelcomeBatchQuery
{
    Task<LeagueWelcomeBatchData> ExecuteAsync(DateTime windowStartUtc, DateTime nowUtc, CancellationToken cancellationToken);
}
