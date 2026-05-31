using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Onboarding.Commands;

public class DismissOnboardingCommandHandler(IOnboardingSkipRepository onboardingSkipRepository)
    : IRequestHandler<DismissOnboardingCommand>
{
    public async Task Handle(DismissOnboardingCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);

        // Dismiss = skip every optional step. Required steps can't be skipped, and any future
        // step (new key) won't be in the skips, so the checklist will quietly reappear for it.
        await onboardingSkipRepository.AddSkipsAsync(request.UserId, OnboardingStepRegistry.OptionalKeys, cancellationToken);
    }
}
