using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetSeasonPassOptionsQuery(string UserId, int SeasonId) : IRequest<SeasonPassOptionsDto?>;
