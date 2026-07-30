namespace ThePredictions.Domain.Common.Exceptions;

/// <summary>
/// Thrown when a request is refused by a business rule - the caller asked for something the current state
/// does not allow ("Only pending members can be approved", "The entry deadline for this league has
/// passed"). The API error middleware maps this to 400 Bad Request and logs it as a Warning, because the
/// fault lies with the request rather than the server.
/// <para>
/// Use this instead of <see cref="InvalidOperationException"/> for every rule the caller could have
/// satisfied. <see cref="InvalidOperationException"/> now means a server-side defect - a missing setting,
/// a misused API, a result set that will not materialise - and is logged as an Error with a 500 response.
/// That way a fault nobody has classified is reported as a server problem, which is the safe default: the
/// alternative hides real breakage in the client-error bucket where no alert looks for it.
/// </para>
/// </summary>
public class BusinessRuleViolationException(string message) : Exception(message);
