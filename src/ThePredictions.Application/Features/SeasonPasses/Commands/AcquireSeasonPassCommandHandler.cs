using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Guards;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.SeasonPasses.Commands;

public class AcquireSeasonPassCommandHandler(
    ISeasonRepository seasonRepository,
    ISeasonPassRepository seasonPassRepository,
    IUserManager userManager,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AcquireSeasonPassCommand>
{
    public async Task Handle(AcquireSeasonPassCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);
        Guard.Against.NegativeOrZero(request.SeasonId);

        // The user must exist. The email-confirmation gate from ADR 0009(b) is temporarily suspended
        // (ADR 0012) until verification emails ship, so an unconfirmed email no longer blocks acquisition.
        var user = await userManager.FindByIdAsync(request.UserId);
        Guard.Against.EntityNotFound(request.UserId, user, nameof(ApplicationUser));

        // Idempotent: the user already holds a pass for this season.
        if (await seasonPassRepository.ExistsForUserSeasonAsync(request.UserId, request.SeasonId, cancellationToken))
            return;

        var season = await seasonRepository.GetByIdAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, nameof(Season));

        // Free season -> acquire a £0 Free pass.
        if (!season!.RequiresPayment)
        {
            await seasonPassRepository.AddAsync(SeasonPass.CreateFree(request.UserId, request.SeasonId, dateTimeProvider), cancellationToken);
            return;
        }

        // Paid season, but the user's first ever season -> free trial.
        var existingRecordCount = await seasonPassRepository.CountForUserAsync(request.UserId, cancellationToken);
        if (existingRecordCount == 0)
        {
            await seasonPassRepository.AddAsync(SeasonPass.CreateTrial(request.UserId, request.SeasonId, dateTimeProvider), cancellationToken);
            return;
        }

        // Otherwise the pass must be paid for -> handled by Stripe checkout (Phase B), not this free-acquire path.
        throw new InvalidOperationException($"Season (ID: {request.SeasonId}) requires payment; acquire it via checkout.");
    }
}
