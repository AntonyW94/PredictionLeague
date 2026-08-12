namespace ThePredictions.Application.Features.Onboarding.Queries;

/// <summary>Reads what a player has done so far, for the checklist that gets them started.</summary>
/// <remarks>
/// The phone number arrives as stored rather than as a yes-or-no, because deciding that a blank one does not count is a rule -
/// and it was <c>LEN(LTRIM(RTRIM(...))) &gt; 0</c> inside an <c>EXISTS</c>.
/// </remarks>
public interface IOnboardingStateQuery
{
    Task<OnboardingStateRow> ExecuteAsync(string userId, CancellationToken cancellationToken);

    /// <summary>The checklist steps this player has dismissed.</summary>
    Task<IReadOnlyList<string>> GetSkippedStepKeysAsync(string userId, CancellationToken cancellationToken);
}
