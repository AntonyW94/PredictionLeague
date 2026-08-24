namespace ThePredictions.Application.Data;

/// <summary>
/// Decides the isolation level every query-side read runs at, by wrapping the read's SQL.
/// </summary>
/// <remarks>
/// Injected rather than hard-coded into the read connection for two reasons: the wrapping it applies is
/// dialect-specific, and the level the whole query side runs at is an operational decision that should be
/// changeable in one registration rather than in every query file.
/// </remarks>
public interface IReadIsolationPolicy
{
    /// <summary>
    /// Returns the batch to execute in place of <paramref name="sql"/>. The read's own result set must
    /// remain the batch's first, because that is the one Dapper materialises.
    /// </summary>
    string Apply(string sql);
}
