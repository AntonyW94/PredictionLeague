using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Boosts;

namespace ThePredictions.Application.Features.Boosts.Queries;

/// <summary>Lists every boost definition for the league create/edit boost-selection UI.</summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetBoostCatalogueQuery : IRequest<List<BoostCatalogueItemDto>>;
