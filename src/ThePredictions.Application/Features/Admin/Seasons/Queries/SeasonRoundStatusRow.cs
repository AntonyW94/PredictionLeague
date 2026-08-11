using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>One round of one season, and what state it is in.</summary>
/// <remarks>
/// The status arrives typed. The statements this replaces counted each state with its own correlated subquery and a text
/// literal - <c>AND r.[Status] = 'Draft'</c> and three more - so a renamed status would have started returning zero
/// rather than failing.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Data-only type: properties only, no logic to test.")]
public sealed record SeasonRoundStatusRow(int SeasonId, int RoundNumber, RoundStatus Status);
