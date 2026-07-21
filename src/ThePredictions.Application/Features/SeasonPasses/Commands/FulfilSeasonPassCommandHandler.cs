using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public class FulfilSeasonPassCommandHandler(
    ISeasonRepository seasonRepository,
    ISeasonPassRepository seasonPassRepository,
    IDateTimeProvider dateTimeProvider,
    ILogger<FulfilSeasonPassCommandHandler> logger) : IRequestHandler<FulfilSeasonPassCommand>
{
    public async Task Handle(FulfilSeasonPassCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);
        Guard.Against.NegativeOrZero(request.SeasonId);
        Guard.Against.NullOrWhiteSpace(request.PaymentReference);

        // Idempotent: webhook retries (and the unique (UserId, SeasonId) index) must not create duplicate passes.
        if (await seasonPassRepository.ExistsForUserSeasonAsync(request.UserId, request.SeasonId, cancellationToken))
        {
            logger.LogInformation("Season pass already exists for user (ID: {UserId}) in season (ID: {SeasonId}); ignoring duplicate fulfilment.",
                request.UserId, request.SeasonId);
            return;
        }

        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, nameof(Season));

        var seasonPass = SeasonPass.CreatePurchased(
            request.UserId, request.SeasonId, request.Tier, request.AmountPaid, request.SmsFeePaid, request.PaymentReference, dateTimeProvider);

        await seasonPassRepository.AddAsync(seasonPass, cancellationToken);

        logger.LogInformation("Season pass purchased for user (ID: {UserId}) in season (ID: {SeasonId}).",
            request.UserId, request.SeasonId);
    }
}
