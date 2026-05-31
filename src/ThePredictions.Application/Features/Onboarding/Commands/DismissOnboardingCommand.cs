using MediatR;

namespace ThePredictions.Application.Features.Onboarding.Commands;

public record DismissOnboardingCommand(string UserId) : IRequest;
