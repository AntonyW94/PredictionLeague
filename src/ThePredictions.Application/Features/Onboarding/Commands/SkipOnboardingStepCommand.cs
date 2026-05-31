using MediatR;

namespace ThePredictions.Application.Features.Onboarding.Commands;

public record SkipOnboardingStepCommand(string UserId, string StepKey) : IRequest;
