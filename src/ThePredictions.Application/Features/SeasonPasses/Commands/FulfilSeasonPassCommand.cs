using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public record FulfilSeasonPassCommand(
    string UserId,
    int SeasonId,
    SeasonPassTier Tier,
    decimal AmountPaid,
    decimal SmsFeePaid,
    string PaymentReference) : IRequest;
