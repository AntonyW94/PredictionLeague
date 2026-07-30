using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Application.Features.Onboarding.Commands;

public class SkipOnboardingStepCommandHandler(IOnboardingSkipRepository onboardingSkipRepository)
    : IRequestHandler<SkipOnboardingStepCommand>
{
    public async Task Handle(SkipOnboardingStepCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(request.UserId);
        Guard.Against.NullOrWhiteSpace(request.StepKey);

        if (!OnboardingStepRegistry.OptionalKeys.Contains(request.StepKey))
            throw new BusinessRuleViolationException($"Onboarding step '{request.StepKey}' cannot be skipped.");

        await onboardingSkipRepository.AddSkipsAsync(request.UserId, [request.StepKey], cancellationToken);
    }
}
