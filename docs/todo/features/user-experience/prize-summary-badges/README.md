# Feature: Prize Summary Badges

## Status

**Not Started** | In Progress | Complete

## Summary

Add gamified summary statistics to the collapsed prize section headers on the league dashboard. When collapsed, each prize section (Round Prizes, Monthly Prizes, End of Season Prizes) will display colourful badge pills showing key stats like wins, earnings, and remaining opportunities. This gives users instant insight into their prize performance without needing to expand each section.

## User Story

As a league member, I want to see a quick summary of my prize performance in each category so that I can instantly understand how I'm doing without expanding every section.

## Design / Mockup

### Round Prizes & Monthly Prizes (Collapsed)

```
┌─────────────────────────────────────────────────┐
│              Round Prizes                       │
│                                                 │
│   [🏆 2 wins]  [💰 £15.00]  [⏳ 8 left]         │
│                                                 │
│                    ∨                            │
└─────────────────────────────────────────────────┘
```

### End of Season Prizes (Collapsed) - In Prize Position

```
┌─────────────────────────────────────────────────┐
│           End of Season Prizes                  │
│                                                 │
│        [🥇 On track for £50.00]                 │
│                                                 │
│                    ∨                            │
└─────────────────────────────────────────────────┘
```

### End of Season Prizes (Collapsed) - Outside Prize Position

```
┌─────────────────────────────────────────────────┐
│           End of Season Prizes                  │
│                                                 │
│          [🎯 12 pts behind 3rd]                 │
│                                                 │
│                    ∨                            │
└─────────────────────────────────────────────────┘
```

### Badge Colours

| Stat Type | Background | Icon |
|-----------|------------|------|
| Wins | Gold (`--gold` / `#FFD700`) | 🏆 |
| Money earned | Green (`--green-600`) | 💰 |
| Remaining | Blue (`--blue-500`) | ⏳ |
| Prize position (gold) | Gold (`--gold`) | 🥇 |
| Prize position (silver) | Silver (`--silver`) | 🥈 |
| Prize position (bronze) | Bronze (`--bronze`) | 🥉 |
| Outside prizes | Purple (`--purple-300`) | 🎯 |

### Behaviour

- Summary row is **visible when collapsed**, **hidden when expanded**
- Smooth fade transition when toggling (match existing 0.3s ease)
- Badges are horizontally centred in the header
- Show "0 wins" when user has no wins (don't hide the badge)

## Acceptance Criteria

- [ ] Round Prizes section shows: wins count, money earned, rounds left
- [ ] Monthly Prizes section shows: wins count, money earned, months left
- [ ] End of Season section shows: prize tracking status OR points behind next prize
- [ ] Badges use correct colours (gold for wins, green for money, blue for remaining)
- [ ] Badges use emoji icons (🏆 💰 ⏳ 🥇 🥈 🥉 🎯)
- [ ] Summary row hides smoothly when section is expanded
- [ ] Users with 0 wins see "0 wins" badge (not hidden)
- [ ] End of Season badges use gold/silver/bronze colours when in prize position
- [ ] Works correctly on mobile (badges wrap if needed)

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [CSS Badge Components](./01-css-badge-components.md) | Create reusable badge pill styles | Not Started |
| 2 | [Round Prizes Section](./02-round-prizes-section.md) | Add summary badges to Round Prizes tile | Not Started |
| 3 | [Monthly Prizes Section](./03-monthly-prizes-section.md) | Add summary badges to Monthly Prizes tile | Not Started |
| 4 | [End of Season Section](./04-end-of-season-section.md) | Add prize tracking badge to End of Season tile | Not Started |

## Dependencies

- [x] WinningsDto already contains all required prize data
- [x] Collapsible section CSS already exists
- [x] Gold/silver/bronze colours defined in variables.css
- [ ] Need access to overall leaderboard data in WinningsSection for End of Season calculations

## Technical Notes

### Data Already Available

The `WinningsDto` (fetched by `WinningsSection.razor`) contains:

```csharp
public class WinningsDto
{
    public List<PrizeDto> RoundPrizes { get; set; }      // Each has UserId if won
    public List<PrizeDto> MonthlyPrizes { get; set; }   // Each has UserId if won
    public List<PrizeDto> EndOfSeasonPrizes { get; init; }
    public WinningsLeaderboardDto Leaderboard { get; init; }
}
```

### Calculating Stats from Existing Data

**Round/Monthly Prizes (in Razor):**
```csharp
// Count wins for current user
int winsCount = Prizes.Count(p => p.UserId == currentUserId);

// Sum money earned
decimal earned = Prizes.Where(p => p.UserId == currentUserId).Sum(p => p.Amount);

// Count remaining (TBC)
int remaining = Prizes.Count(p => p.Winner == null);
```

**End of Season Prize Position:**

This requires knowing:
1. Current user's rank in overall standings
2. Number of "Overall" type prizes configured
3. Points of the person at the lowest prize position

The `WinningsSection` will need to also fetch overall leaderboard data to calculate the points gap.

### Current User ID

Available via `BaseLeaderboardComponent` which the prize tiles inherit from. Access via the authentication state.

## Open Questions

- [x] Should "0 wins" be shown or hidden? → **Show "0 wins"**
- [x] What colours for each badge type? → **Gold, Green, Blue as specified**
- [x] Icons: emoji or SVG? → **Emoji (🏆 💰 ⏳)**
- [x] Animation for hide/show? → **Yes, smooth fade transition**
