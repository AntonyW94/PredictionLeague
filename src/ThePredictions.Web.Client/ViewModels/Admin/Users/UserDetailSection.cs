namespace ThePredictions.Web.Client.ViewModels.Admin.Users;

/// <summary>
/// Which of an account's details the popup is showing.
/// </summary>
/// <remarks>
/// Five figures on the card open four of these: the pass count and the pass spend both open
/// <see cref="SeasonPasses"/>, and the joined count and the entry spend both open <see cref="Memberships"/>. The money
/// and the count are two views of one set of rows, so sending them to one popup is not a shortcut - two popups listing
/// the same leagues would be the thing that needed explaining.
/// </remarks>
public enum UserDetailSection
{
    Memberships,
    AdministeredLeagues,
    SeasonPasses,
    Prizes,
    Badges,
    Onboarding
}
