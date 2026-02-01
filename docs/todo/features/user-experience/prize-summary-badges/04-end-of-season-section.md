# Task 4: End of Season Section

**Parent Feature:** [Prize Summary Badges](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add a prize tracking badge to the End of Season Prizes tile that shows either the prize the user is on track to win (if in a prize position) or how many points behind the nearest prize position they are.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `Components/Pages/Leagues/Dashboard/WinningsSection.razor` | Modify | Fetch overall leaderboard data |
| `Components/Pages/Leagues/Dashboard/EndOfSeasonPrizesTile.razor` | Modify | Add prize tracking badge |

## Implementation Steps

### Step 1: Fetch Overall Leaderboard in WinningsSection

The `WinningsSection.razor` needs to fetch the overall leaderboard to calculate prize position tracking. Add a new service call alongside the existing winnings call.

```razor
@inject ILeagueService LeagueService
@inject AuthenticationStateProvider AuthStateProvider

@code {
    [Parameter, EditorRequired]
    public int LeagueId { get; set; }

    private WinningsDto? _winningsData;
    private IEnumerable<LeaderboardEntryDto>? _overallLeaderboard;  // NEW
    private string? _currentUserId;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Get current user ID
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            _currentUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Fetch both in parallel for performance
            var winningsTask = LeagueService.GetWinningsAsync(LeagueId);
            var leaderboardTask = LeagueService.GetOverallLeaderboardAsync(LeagueId);

            await Task.WhenAll(winningsTask, leaderboardTask);

            _winningsData = await winningsTask;
            _overallLeaderboard = await leaderboardTask;
        }
        catch (Exception)
        {
            // Handle error if needed
        }
        finally
        {
            _isLoading = false;
        }
    }
}
```

### Step 2: Create EndOfSeasonSummary DTO

Create a simple class to hold the calculated summary data. This can be a nested class within the tile or a separate record.

```csharp
private record EndOfSeasonSummary(
    bool IsInPrizePosition,
    int? CurrentRank,
    int? PointsBehindNextPrize,
    int? NextPrizeRank,
    decimal? PotentialPrize,
    string? MedalType  // "gold", "silver", "bronze", or null
);
```

### Step 3: Add Calculation Logic to EndOfSeasonPrizesTile

Add new parameters and calculation logic.

```csharp
@code {
    [Parameter] public List<PrizeDto>? Prizes { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool IsExpandedByDefault { get; set; } = false;
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public IEnumerable<LeaderboardEntryDto>? OverallLeaderboard { get; set; }  // NEW

    private bool _isExpanded;

    private EndOfSeasonSummary? GetSummary()
    {
        if (Prizes == null || !Prizes.Any() || OverallLeaderboard == null || string.IsNullOrEmpty(CurrentUserId))
            return null;

        var leaderboardList = OverallLeaderboard.ToList();

        // Find current user in leaderboard
        var currentUserEntry = leaderboardList.FirstOrDefault(e => e.UserId == CurrentUserId);
        if (currentUserEntry == null)
            return null;

        var currentRank = (int)currentUserEntry.Rank;
        var currentPoints = currentUserEntry.TotalPoints;

        // Get "Overall" type prizes (filter out MostExactScores if present)
        // Overall prizes have names like "1st Place", "2nd Place", etc.
        var overallPrizes = Prizes
            .Where(p => p.Name.Contains("Place") || p.Name.Contains("1st") || p.Name.Contains("2nd") || p.Name.Contains("3rd"))
            .OrderBy(p => p.Name)
            .ToList();

        if (!overallPrizes.Any())
            return null;

        // Number of prize positions
        int prizePositionCount = overallPrizes.Count;

        // Is user in a prize position?
        bool isInPrizePosition = currentRank <= prizePositionCount;

        if (isInPrizePosition)
        {
            // Find the prize they would win
            var prize = overallPrizes.ElementAtOrDefault(currentRank - 1);
            var medalType = currentRank switch
            {
                1 => "gold",
                2 => "silver",
                3 => "bronze",
                _ => null
            };

            return new EndOfSeasonSummary(
                IsInPrizePosition: true,
                CurrentRank: currentRank,
                PointsBehindNextPrize: null,
                NextPrizeRank: null,
                PotentialPrize: prize?.Amount,
                MedalType: medalType
            );
        }
        else
        {
            // Find the person at the lowest prize position
            var lowestPrizeEntry = leaderboardList.FirstOrDefault(e => e.Rank == prizePositionCount);
            if (lowestPrizeEntry == null)
                return null;

            int pointsBehind = lowestPrizeEntry.TotalPoints - currentPoints;

            return new EndOfSeasonSummary(
                IsInPrizePosition: false,
                CurrentRank: currentRank,
                PointsBehindNextPrize: pointsBehind,
                NextPrizeRank: prizePositionCount,
                PotentialPrize: null,
                MedalType: null
            );
        }
    }

    // ... existing code ...
}
```

### Step 4: Update the Header Markup

```razor
@{
    var summary = GetSummary();
}

<div class="section-header-collapsible @(_isExpanded ? "expanded" : "")" @onclick="ToggleExpanded">
    <h5 class="text-white fw-bold text-center text-md-start">End of Season Prizes</h5>

    @if (!IsLoading && summary != null)
    {
        <div class="section-header-summary">
            @if (summary.IsInPrizePosition)
            {
                @* In prize position - show medal and potential prize *@
                var badgeClass = summary.MedalType switch
                {
                    "gold" => "prize-badge-gold",
                    "silver" => "prize-badge-silver",
                    "bronze" => "prize-badge-bronze",
                    _ => "prize-badge-green"
                };
                var medalIcon = summary.MedalType switch
                {
                    "gold" => "🥇",
                    "silver" => "🥈",
                    "bronze" => "🥉",
                    _ => "🏆"
                };

                <span class="prize-badge @badgeClass">
                    <span class="prize-badge-icon">@medalIcon</span>
                    <span class="prize-badge-text">
                        On track for @(summary.PotentialPrize?.ToString("C", new System.Globalization.CultureInfo("en-GB")) ?? "a prize")
                    </span>
                </span>
            }
            else
            {
                @* Outside prize position - show points behind *@
                <span class="prize-badge prize-badge-purple">
                    <span class="prize-badge-icon">🎯</span>
                    <span class="prize-badge-text">
                        @summary.PointsBehindNextPrize pts behind @GetOrdinal(summary.NextPrizeRank ?? 0)
                    </span>
                </span>
            }
        </div>
    }

    <i class="bi bi-chevron-down text-white collapse-icon @(_isExpanded ? "rotated" : "")"></i>
</div>
```

### Step 5: Add Ordinal Helper Method

```csharp
private static string GetOrdinal(int number)
{
    if (number <= 0) return number.ToString();

    return (number % 100) switch
    {
        11 or 12 or 13 => $"{number}th",
        _ => (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        }
    };
}
```

### Step 6: Update WinningsSection to Pass New Parameters

```razor
<EndOfSeasonPrizesTile
    Prizes="@_winningsData?.EndOfSeasonPrizes"
    IsLoading="@_isLoading"
    CurrentUserId="@_currentUserId"
    OverallLeaderboard="@_overallLeaderboard" />
```

## Code Patterns to Follow

The `GetOverallLeaderboardAsync` method already exists in `ILeagueService` and is used by `OverallLeaderboardTile.razor`. Follow the same pattern.

```csharp
// From ILeagueService
Task<IEnumerable<LeaderboardEntryDto>> GetOverallLeaderboardAsync(int leagueId);
```

The `LeaderboardEntryDto` contains:
- `Rank` (long)
- `TotalPoints` (int)
- `UserId` (string)
- `PlayerName` (string)

## Verification

- [ ] Badge shows "On track for £X" when user is in prize position (ranks 1-N where N is number of prizes)
- [ ] Badge uses gold colour and 🥇 for 1st place
- [ ] Badge uses silver colour and 🥈 for 2nd place
- [ ] Badge uses bronze colour and 🥉 for 3rd place
- [ ] Badge uses green colour and 🏆 for 4th+ prize positions
- [ ] Badge shows "X pts behind Yth" when user is outside prizes
- [ ] Purple badge with 🎯 icon for outside-prize state
- [ ] Ordinals are correct (1st, 2nd, 3rd, 4th, 11th, 12th, 13th, 21st, etc.)
- [ ] Summary hides when section is expanded
- [ ] No errors when leaderboard or prizes are null/empty
- [ ] Handles case where user is 1st place (should show gold "On track for £X")

## Edge Cases to Consider

- User is in 1st place → Show gold badge with 🥇 and 1st place prize amount
- User is in 4th place with 4 prizes configured → Show green badge with 🏆
- User is in 5th place with 3 prizes → Show "X pts behind 3rd"
- Tied points at prize boundary → Show points behind (may be 0)
- No overall prizes configured → Don't show badge
- "Most Exact Scores" prize exists alongside "Overall" → Filter correctly
- Season just started, no points yet → All users tied at 0 points

## Notes

- The `EndOfSeasonPrizes` list may contain both "Overall" prizes (1st, 2nd, 3rd Place) and "Most Exact Scores" prize
- We only consider "Overall" type prizes for position tracking
- Prize names follow pattern: "1st Place", "2nd Place", "3rd Place" - use this to filter
- The leaderboard is already sorted by rank, so we can use `FirstOrDefault` to find users at specific ranks
- Consider caching the `GetSummary()` result if performance becomes an issue (unlikely with small data)
