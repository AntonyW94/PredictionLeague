using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Application.Features.Onboarding.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetOnboardingChecklistQuery(string UserId) : IRequest<OnboardingChecklistDto>;
