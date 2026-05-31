using MediatR;
using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Application.Features.Onboarding.Queries;

public record GetOnboardingChecklistQuery(string UserId) : IRequest<OnboardingChecklistDto>;
