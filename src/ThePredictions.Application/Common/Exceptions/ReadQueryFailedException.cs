namespace ThePredictions.Application.Common.Exceptions;

/// <summary>
/// Thrown when a read query fails for a server-side reason - most commonly because the result set does
/// not match the result record Dapper was asked to materialise, or because a single-row query returned
/// more than one row. Dapper reports both as a plain <see cref="InvalidOperationException"/>, which the
/// API error middleware maps to 400 Bad Request and logs as a Warning; wrapping them in a distinct type
/// keeps a server-side defect out of the client-error bucket so it surfaces as a 500 and an Error.
/// </summary>
public class ReadQueryFailedException(Exception innerException)
    : Exception($"A read query failed to execute or materialise: {innerException.Message}", innerException);
