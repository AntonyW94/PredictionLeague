using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record HasSeasonPredictionsQuery(int SeasonId) : IRequest<bool>;
