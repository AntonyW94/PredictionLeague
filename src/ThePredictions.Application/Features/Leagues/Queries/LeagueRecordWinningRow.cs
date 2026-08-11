using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

/// <summary>
/// One prize paid out in the league, with the parts its label is built from.
/// </summary>
/// <remarks>
/// The label is <c>PrizeDescription.For</c>, not a column: the old statement built it in SQL and finished with
/// <c>DATENAME(MONTH, ...)</c>, whose output depends on the language the database login is configured with.
///
/// <see cref="PrizeType"/> is the enum rather than the raw column because the column is a lie in an interesting
/// way - it is declared <c>nvarchar(20)</c> and documented as holding names like "Monthly", but the write path
/// passes the enum and its numeric value is what lands, so it actually holds "0" to "4". The old comparison
/// worked only because SQL Server silently converted the string to an int; on a case-sensitive or stricter engine
/// it would not. Mapping it here means the next adapter has to produce a real <c>PrizeType</c> and cannot inherit
/// the accident.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record LeagueRecordWinningRow(
    string UserId,
    string FirstName,
    string LastName,
    decimal Amount,
    DateTime AwardedDateUtc,
    PrizeType PrizeType,
    string? PrizeDescription,
    int? RoundNumber,
    int? Month);
