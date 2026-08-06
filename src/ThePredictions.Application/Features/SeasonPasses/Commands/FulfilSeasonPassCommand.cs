using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record FulfilSeasonPassCommand(
    string UserId,
    int SeasonId,
    SeasonPassTier Tier,
    decimal AmountPaid,
    decimal SmsFeePaid,
    string PaymentReference) : IRequest;
