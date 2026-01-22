# Task 2: Round Prizes Section

**Parent Feature:** [Prize Summary Badges](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the prize summary badges row to the Round Prizes collapsible tile, showing wins count, money earned, and rounds remaining.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `Components/Pages/Leagues/Dashboard/RoundPrizesTile.razor` | Modify | Add summary badges to header |

## Implementation Steps

### Step 1: Add CurrentUserId Parameter

The tile needs access to the current user's ID to calculate their personal stats. Add a parameter to receive this from the parent component.

```csharp
@code {
    [Parameter] public List<PrizeDto>? Prizes { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool IsExpandedByDefault { get; set; } = false;
    [Parameter] public string? CurrentUserId { get; set; }  // NEW

    private bool _isExpanded;

    // ... rest of existing code
}
```

### Step 2: Add Computed Properties for Stats

Add properties to calculate the badge values from the Prizes list.

```csharp
@code {
    // ... existing code ...

    private int WinsCount => Prizes?.Count(p => p.UserId == CurrentUserId) ?? 0;

    private decimal MoneyEarned => Prizes?
        .Where(p => p.UserId == CurrentUserId)
        .Sum(p => p.Amount) ?? 0;

    private int RoundsLeft => Prizes?.Count(p => p.Winner == null) ?? 0;
}
```

### Step 3: Update the Header Markup

Modify the `section-header-collapsible` div to include the summary row and track expanded state.

**Current:**
```razor
<div class="section-header-collapsible" @onclick="ToggleExpanded">
    <h5 class="text-white fw-bold text-center text-md-start">Round Prizes</h5>
    <i class="bi bi-chevron-down text-white collapse-icon @(_isExpanded ? "rotated" : "")"></i>
</div>
```

**Updated:**
```razor
<div class="section-header-collapsible @(_isExpanded ? "expanded" : "")" @onclick="ToggleExpanded">
    <h5 class="text-white fw-bold text-center text-md-start">Round Prizes</h5>

    @if (!IsLoading && Prizes?.Any() == true)
    {
        <div class="section-header-summary">
            <span class="prize-badge prize-badge-gold">
                <span class="prize-badge-icon">🏆</span>
                <span class="prize-badge-text">@WinsCount wins</span>
            </span>
            <span class="prize-badge prize-badge-green">
                <span class="prize-badge-icon">💰</span>
                <span class="prize-badge-text">@MoneyEarned.ToString("C", new System.Globalization.CultureInfo("en-GB"))</span>
            </span>
            <span class="prize-badge prize-badge-blue">
                <span class="prize-badge-icon">⏳</span>
                <span class="prize-badge-text">@RoundsLeft left</span>
            </span>
        </div>
    }

    <i class="bi bi-chevron-down text-white collapse-icon @(_isExpanded ? "rotated" : "")"></i>
</div>
```

### Step 4: Update WinningsSection to Pass CurrentUserId

The parent `WinningsSection.razor` needs to pass the current user's ID to the tile.

In `WinningsSection.razor`, add injection and pass the parameter:

```razor
@inject AuthenticationStateProvider AuthStateProvider

@code {
    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        // Get current user ID
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        _currentUserId = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // ... existing winnings loading code ...
    }
}
```

Then update the tile usage:

```razor
<RoundPrizesTile
    Prizes="@_winningsData?.RoundPrizes"
    IsLoading="@_isLoading"
    CurrentUserId="@_currentUserId" />
```

## Code Patterns to Follow

The `BaseLeaderboardComponent` that prize tiles inherit from has a `GetUserHighlightClass` method that already checks user IDs. Follow the same pattern for consistency.

Looking at `BaseLeaderboardComponent.cs`:

```csharp
protected string GetUserHighlightClass(string? userId)
{
    return userId == _currentUserId ? "current-user-highlight" : "";
}
```

The user ID is obtained from the authentication state provider.

## Verification

- [ ] Summary badges appear when section is collapsed
- [ ] Summary badges hide smoothly when section is expanded
- [ ] Wins count shows correct number (including 0)
- [ ] Money earned shows correctly formatted GBP (e.g., "£15.00")
- [ ] Rounds left shows correct count of prizes with no winner
- [ ] Badges are horizontally centred
- [ ] No errors when Prizes is null or empty
- [ ] Component still works when CurrentUserId is null (guest/logged out)

## Edge Cases to Consider

- User has 0 wins → Show "0 wins" badge (not hidden)
- User has won all available prizes → "0 left" is valid
- No prizes configured → Summary row should not render
- Large money amounts → "£1,234.56" should not break layout
- Loading state → Summary row should not show while loading

## Notes

- The `CultureInfo("en-GB")` ensures consistent GBP formatting with £ symbol
- The summary only shows when `!IsLoading && Prizes?.Any() == true` to avoid flicker
- The `.expanded` class on the header controls the summary row visibility via CSS
