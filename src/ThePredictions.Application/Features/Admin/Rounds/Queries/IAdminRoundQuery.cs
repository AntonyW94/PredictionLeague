namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>Reads one round for the administrator's editor, or nothing if there is no such round.</summary>
/// <remarks>
/// Null rather than an exception: whether a missing round is a client mistake or a server fault is the handler's to
/// decide, and it decides "not found".
///
/// The fixtures are a separate read. They used to arrive in the same statement through a left join, which meant a round
/// with no fixtures came back as one row of nulls that the mapping then had to recognise and discard, and every joined
/// column had to be nullable whether it could be null or not.
/// </remarks>
public interface IAdminRoundQuery
{
    Task<AdminRoundRow?> ExecuteAsync(int roundId, CancellationToken cancellationToken);
}
