using Ardalis.GuardClauses;
using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public class AcquireSeasonPassCommandHandler(
    ISeasonRepository seasonRepository,
    ISeasonPassRepository seasonPassRepository,
    IUserManager userManager,
    IDateTimeProvider dateTimeProvider,
    ILogger<AcquireSeasonPassCommandHandler> logger) : IRequestHandler<AcquireSeasonPassCommand>
{
    public async Task Handle(AcquireSeasonPassCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);
        Guard.Against.NegativeOrZero(request.SeasonId);

        // A confirmed email is required to take part (ADR 0009(b), re-enabled by ADR 0014 now that
        // verification emails ship). Google-OAuth users are already confirmed. Existing users were
        // grandfathered to confirmed, so this only blocks new, unverified sign-ups.
        var user = await userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, nameof(ApplicationUser));

        if (!user!.EmailConfirmed)
            throw new EmailNotConfirmedException();

        // Idempotent: the user already holds a pass for this season.
        if (await seasonPassRepository.ExistsForUserSeasonAsync(request.UserId, request.SeasonId, cancellationToken))
        {
            logger.LogInformation(
                "Season Pass acquire skipped: User (ID: {UserId}) already holds a pass for Season (ID: {SeasonId}).",
                request.UserId,
                request.SeasonId);
            return;
        }

        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, nameof(Season));

        // Free season -> acquire a £0 Free pass.
        if (!season!.RequiresPayment)
        {
            await seasonPassRepository.AddAsync(SeasonPass.CreateFree(request.UserId, request.SeasonId, dateTimeProvider), cancellationToken);

            logger.LogInformation(
                "Season Pass granted (Free) to User (ID: {UserId}) for Season (ID: {SeasonId}).",
                request.UserId,
                request.SeasonId);
            return;
        }

        // Paid season, but the user's first ever season -> free trial.
        var existingRecordCount = await seasonPassRepository.CountForUserAsync(request.UserId, cancellationToken);
        if (existingRecordCount == 0)
        {
            await seasonPassRepository.AddAsync(SeasonPass.CreateTrial(request.UserId, request.SeasonId, dateTimeProvider), cancellationToken);

            logger.LogInformation(
                "Season Pass granted (Trial) to User (ID: {UserId}) for Season (ID: {SeasonId}).",
                request.UserId,
                request.SeasonId);
            return;
        }

        // Otherwise the pass must be paid for -> handled by Stripe checkout (Phase B), not this free-acquire path.
        throw new BusinessRuleViolationException($"Season (ID: {request.SeasonId}) requires payment; acquire it via checkout.");
    }
}
