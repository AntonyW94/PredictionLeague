using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Application.Features.Onboarding.Queries;

public class GetOnboardingChecklistQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetOnboardingChecklistQuery, OnboardingChecklistDto>
{
    public async Task<OnboardingChecklistDto> Handle(GetOnboardingChecklistQuery request, CancellationToken cancellationToken)
    {
        const string stateSql = @"
            SELECT
                (SELECT COUNT(*) FROM [SeasonPasses] WHERE [UserId] = @UserId) AS PassCount,
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [UserId] = @UserId) AS LeagueCount,
                CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM [AspNetUsers]
                    WHERE [Id] = @UserId AND [PhoneNumber] IS NOT NULL AND LEN(LTRIM(RTRIM([PhoneNumber]))) > 0
                ) THEN 1 ELSE 0 END AS BIT) AS HasMobile,
                CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM [UserPayoutDetails]
                    WHERE [UserId] = @UserId
                ) THEN 1 ELSE 0 END AS BIT) AS HasPayoutDetails;";

        var state = await dbConnection.QuerySingleOrDefaultAsync<OnboardingUserState>(stateSql, cancellationToken, new { request.UserId })
                    ?? new OnboardingUserState(0, 0, false, false);

        const string skipsSql = "SELECT [StepKey] FROM [UserOnboardingSkips] WHERE [UserId] = @UserId;";
        var skippedKeys = (await dbConnection.QueryAsync<string>(skipsSql, cancellationToken, new { request.UserId })).ToHashSet();

        return OnboardingStepRegistry.Build(state, skippedKeys);
    }
}
