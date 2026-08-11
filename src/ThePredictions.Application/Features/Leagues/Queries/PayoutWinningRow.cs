using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One prize won in the league, with who won it and what kind of prize it was.
/// </summary>
/// <remarks>
/// Name parts, not a formatted name. This screen shows the winner's <b>full</b> name rather than the abbreviated form
/// used elsewhere - an administrator paying real money needs to match the name on a bank account - so the formatting is a
/// rule with a different answer here, and it belongs in C# where that can be said.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record PayoutWinningRow(
    string UserId,
    string FirstName,
    string LastName,
    PrizeType PrizeType,
    decimal Amount);
