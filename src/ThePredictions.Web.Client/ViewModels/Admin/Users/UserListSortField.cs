namespace ThePredictions.Web.Client.ViewModels.Admin.Users;

/// <summary>
/// What the administrator can order the account list by.
/// </summary>
/// <remarks>
/// <see cref="PassSpend"/> and <see cref="EntrySpend"/> are separate from <see cref="TotalSpend"/> because the card now
/// shows the two apart, and a total cannot answer either of the questions the split was added for - who has paid for a
/// pass, and who is spending on leagues.
/// </remarks>
public enum UserListSortField
{
    Name,
    LeaguesCreated,
    LeaguesJoined,
    Badges,
    PassSpend,
    EntrySpend,
    TotalSpend,
    Winnings,
    Setup
}
