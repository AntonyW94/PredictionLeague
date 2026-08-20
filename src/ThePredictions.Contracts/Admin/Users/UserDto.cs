using ThePredictions.Contracts.Onboarding;

namespace ThePredictions.Contracts.Admin.Users;

/// <summary>One account, as the administrator's user list shows it.</summary>
/// <remarks>
/// The figures arrive computed, because what counts as money spent or as a league joined is a rule and rules live in the
/// handler. The detail lists arrive alongside them so the popups behind each figure open without a second request - the
/// rows are read in full to produce the figures anyway, so collapsing them and then fetching them again would be work
/// done twice.
///
/// <b>There is no single "has a Season Pass" flag any more</b>, and that is the point. It was true of an account holding
/// nothing but passes for seasons that finished a year ago, which is exactly the account an administrator is looking for.
/// Three separate questions replace it: <see cref="HasCurrentSeasonPass"/> for whether they can play now,
/// <see cref="HasEverPurchasedSeasonPass"/> for whether they have ever paid, and <see cref="HasEverHeldSeasonPass"/> for
/// whether anything has ever happened on the account at all.
/// </remarks>
public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAdmin,
    bool HasLocalPassword,
    List<string> SocialProviders,
    bool EmailConfirmed,
    bool TermsAccepted,
    bool MarketingOptIn,
    bool HasPayoutDetails,
    bool HasEverHeldSeasonPass,
    bool HasCurrentSeasonPass,
    bool HasEverPurchasedSeasonPass,
    int LeaguesCreated,
    int LeaguesJoinedApproved,
    int LeaguesJoinedPending,
    decimal TotalWinnings,
    decimal SeasonPassSpend,
    decimal LeagueEntrySpend,
    OnboardingChecklistDto Onboarding,
    IReadOnlyList<UserLeagueMembershipDto> Memberships,
    IReadOnlyList<UserAdministeredLeagueDto> AdministeredLeagues,
    IReadOnlyList<UserSeasonPassDto> SeasonPasses,
    IReadOnlyList<UserPrizeDto> Prizes,
    IReadOnlyList<UserBadgeDto> Badges
)
{
    /// <summary>Everything this account has paid the site, for passes and league entry together.</summary>
    public decimal TotalSpend => SeasonPassSpend + LeagueEntrySpend;

    /// <summary>
    /// How many different badges this account has earned, which the catalogue caps.
    /// </summary>
    /// <remarks>
    /// Different badges, not awards. A badge that can be won every round is one badge however many times it has been won,
    /// and the handler has already collapsed the repeats - so this counts what it should and cannot exceed the number of
    /// badges that exist.
    /// </remarks>
    public int BadgeCount => Badges.Count;

    /// <summary>How many prizes this account has won, which is not the same as how much.</summary>
    public int PrizeCount => Prizes.Count;

    /// <summary>
    /// The seasons this account holds a pass for that have not finished, newest first.
    /// </summary>
    /// <remarks>
    /// A list rather than a flag because two seasons can be current at once - a league season and a tournament overlap
    /// most summers - and "Pass: yes" would then be hiding which one they had paid for.
    /// </remarks>
    public IReadOnlyList<string> CurrentPassSeasonNames =>
        SeasonPasses
            .Where(pass => pass.IsCurrentSeason)
            .OrderByDescending(pass => pass.SeasonId)
            .Select(pass => pass.SeasonName)
            .ToList();

    /// <summary>How many onboarding steps the data says are done.</summary>
    /// <remarks>
    /// Completed only. A step the account dismissed is not done - that is the whole difference between "would not" and
    /// "has not", and the popup shows which.
    /// </remarks>
    public int OnboardingStepsCompleted =>
        Onboarding.Steps.Count(step => step.State == OnboardingStepStates.Completed);

    /// <summary>How many onboarding steps there are to complete.</summary>
    public int OnboardingStepCount => Onboarding.Steps.Count;

    /// <summary>Whether this account has dealt with every onboarding step, by doing it or by dismissing it.</summary>
    /// <remarks>
    /// Dealt with, not done. A player who dismissed "Add payout details" has answered the question - they do not want to
    /// hand over bank details - so chasing them is chasing somebody who already said no. A skipped step therefore leaves
    /// nothing outstanding, and only a step still Active or Locked does. This is what separates it from
    /// <see cref="OnboardingStepsCompleted"/>, which counts completions and nothing else.
    ///
    /// The judgement is not made here. <see cref="OnboardingChecklistDto.HasOutstandingSteps"/> is computed by the step
    /// registry that owns what each state means, so reading it keeps one definition of "outstanding" rather than a second
    /// one that has to be kept in step. The step-count guard stays: a checklist with no steps at all is data nobody can
    /// interpret, and the safe reading is that the account is not settled, so it surfaces in the filter rather than
    /// vanishing from it.
    /// </remarks>
    public bool OnboardingComplete => OnboardingStepCount > 0 && !Onboarding.HasOutstandingSteps;

    /// <summary>
    /// Nothing has ever happened on this account.
    /// </summary>
    /// <remarks>
    /// Deliberately still keyed on having <b>ever</b> held a pass rather than on holding a current one. An account that
    /// played last season and has not come back is not dormant, it is lapsed, and the two want different chasing - so
    /// "no current pass" is its own filter and this stays meaning "registered and never started".
    /// </remarks>
    public bool IsDormant =>
        !HasEverHeldSeasonPass
        && LeaguesCreated == 0
        && LeaguesJoinedApproved == 0
        && LeaguesJoinedPending == 0;

    /// <summary>
    /// Won money and has given us no way to send it.
    /// </summary>
    /// <remarks>
    /// The one combination on this screen worth acting on the same day: the site cannot pay a prize to an account with no
    /// bank details, and nothing else tells an administrator that has happened.
    /// </remarks>
    public bool IsOwedMoneyWithNowhereToSendIt => TotalWinnings > 0 && !HasPayoutDetails;
}
