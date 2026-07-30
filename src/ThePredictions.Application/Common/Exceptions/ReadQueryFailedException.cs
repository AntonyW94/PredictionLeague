namespace ThePredictions.Application.Common.Exceptions;

/// <summary>
/// Thrown when a read query fails for a server-side reason - most commonly because the result set does
/// not match the result record Dapper was asked to materialise, or because a single-row query returned
/// more than one row. Dapper reports both as a plain <see cref="InvalidOperationException"/>; wrapping
/// them names the failure in the log message rather than leaving a bare Dapper exception to be
/// interpreted.
/// <para>
/// Both types are reported as a 500 and an Error, so this is about diagnosis rather than severity.
/// Business rules use <c>BusinessRuleViolationException</c> for the 400/Warning bucket.
/// </para>
/// </summary>
public class ReadQueryFailedException(Exception innerException)
    : Exception($"A read query failed to execute or materialise: {innerException.Message}", innerException);
