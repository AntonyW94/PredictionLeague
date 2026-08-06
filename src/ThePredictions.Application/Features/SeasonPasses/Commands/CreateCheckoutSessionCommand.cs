using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record CreateCheckoutSessionCommand(string UserId, int SeasonId, SeasonPassTier Tier)
    : IRequest<CreateCheckoutSessionResponse>;
