using MediatR;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public record CreateCheckoutSessionCommand(string UserId, int SeasonId, SeasonPassTier Tier)
    : IRequest<CreateCheckoutSessionResponse>;
