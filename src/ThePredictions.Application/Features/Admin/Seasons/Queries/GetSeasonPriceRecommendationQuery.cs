using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

/// <summary>
/// Asks for a recommended Standard Season Pass price for a season draft. Used by the season
/// create/edit page to pre-fill an editable suggestion. <paramref name="SeasonId"/> is set when
/// editing an existing season (so it is excluded from the cost-horizon and comparable lookups).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetSeasonPriceRecommendationQuery(
    int CompetitionId,
    int NumberOfRounds,
    DateTime StartDateUtc,
    int? SeasonId) : IRequest<PriceRecommendationDto>;
