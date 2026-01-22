# Task 3: Monthly Prizes Section

**Parent Feature:** [Prize Summary Badges](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the prize summary badges row to the Monthly Prizes collapsible tile, showing wins count, money earned, and months remaining.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `Components/Pages/Leagues/Dashboard/MonthlyPrizesTile.razor` | Modify | Add summary badges to header |

## Implementation Steps

### Step 1: Add CurrentUserId Parameter

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

```csharp
@code {
    // ... existing code ...

    private int WinsCount => Prizes?.Count(p => p.UserId == CurrentUserId) ?? 0;

    private decimal MoneyEarned => Prizes?
        .Where(p => p.UserId == CurrentUserId)
        .Sum(p => p.Amount) ?? 0;

    private int MonthsLeft => Prizes?.Count(p => p.Winner == null) ?? 0;
}
```

### Step 3: Update the Header Markup

**Updated header:**

```razor
<div class="section-header-collapsible @(_isExpanded ? "expanded" : "")" @onclick="ToggleExpanded">
    <h5 class="text-white fw-bold text-center text-md-start">Monthly Prizes</h5>

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
                <span class="prize-badge-text">@MonthsLeft left</span>
            </span>
        </div>
    }

    <i class="bi bi-chevron-down text-white collapse-icon @(_isExpanded ? "rotated" : "")"></i>
</div>
```

### Step 4: Update WinningsSection to Pass CurrentUserId

This should already be done as part of Task 2. Just ensure the tile usage is updated:

```razor
<MonthlyPrizesTile
    Prizes="@_winningsData?.MonthlyPrizes"
    IsLoading="@_isLoading"
    CurrentUserId="@_currentUserId" />
```

## Code Patterns to Follow

Identical to Task 2 (Round Prizes Section). The only difference is:
- Section title: "Monthly Prizes"
- Variable name: `MonthsLeft` instead of `RoundsLeft`
- Badge text: Shows "X left" (same wording works for months)

## Verification

- [ ] Summary badges appear when section is collapsed
- [ ] Summary badges hide smoothly when section is expanded
- [ ] Wins count shows correct number (including 0)
- [ ] Money earned shows correctly formatted GBP
- [ ] Months left shows correct count of prizes with no winner
- [ ] Badges are horizontally centred
- [ ] No errors when Prizes is null or empty

## Edge Cases to Consider

- Same as Task 2 - identical handling for null/empty states
- Monthly prizes typically have fewer entries (12 max) than round prizes
- Consider that some months may not have prizes configured

## Notes

- This task is nearly identical to Task 2, so can be implemented quickly after Task 2 is complete
- The same CSS classes and patterns are reused
- Consider extracting common badge rendering to a shared component if a third similar use case emerges
