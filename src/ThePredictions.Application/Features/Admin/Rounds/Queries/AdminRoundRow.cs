using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>One round as the administrator's screens list it, with how many fixtures it holds.</summary>
/// <remarks>
/// The status arrives as the enum rather than the text stored in the column, so nothing has to parse it back. Both
/// statements this replaces selected it as a string and called <c>Enum.Parse</c> on the way out, which turns a bad value
/// in the database into an exception at the edge of the screen rather than at the read.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record AdminRoundRow(
    int Id,
    int SeasonId,
    int RoundNumber,
    string? ApiRoundName,
    DateTime StartDateUtc,
    DateTime DeadlineUtc,
    RoundStatus Status,
    int MatchCount);
