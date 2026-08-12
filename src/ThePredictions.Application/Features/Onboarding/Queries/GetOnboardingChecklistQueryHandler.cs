using MediatR;
using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Application.Features.Onboarding.Queries;

/// <summary>The checklist that gets a new player started, and what they have already ticked off.</summary>
public class GetOnboardingChecklistQueryHandler(IOnboardingStateQuery onboardingStateQuery)
    : IRequestHandler<GetOnboardingChecklistQuery, OnboardingChecklistDto>
{
    public async Task<OnboardingChecklistDto> Handle(GetOnboardingChecklistQuery request, CancellationToken cancellationToken)
    {
        var row = await onboardingStateQuery.ExecuteAsync(request.UserId, cancellationToken);
        var skippedKeys = await onboardingStateQuery.GetSkippedStepKeysAsync(request.UserId, cancellationToken);

        // A number made only of spaces is not a mobile number. The column allows one, and the step should not tick itself for
        // somebody who saved a blank.
        var state = new OnboardingUserState(
            row.PassCount,
            row.LeagueCount,
            HasMobile: !string.IsNullOrWhiteSpace(row.PhoneNumber),
            row.HasPayoutDetails);

        return OnboardingStepRegistry.Build(state, skippedKeys.ToHashSet());
    }
}
