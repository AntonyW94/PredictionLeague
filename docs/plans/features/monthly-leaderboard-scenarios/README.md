# Feature: Monthly Leaderboard Scenarios

## Status

**Not Started** | In Progress | Complete

## Summary

Add real-time "insights" to the league dashboard showing which users are still mathematically in contention to win the round and/or month, along with win/tie probabilities and the specific scenarios (match results) that would lead to each user winning. This feature calculates all possible outcome combinations based on contenders' actual predictions, accounting for exact scores, correct results, and live match states.

## User Story

As a league member, I want to see whether I can still win the round or month during a live gameweek, so that I can follow the remaining matches knowing which results I need.

As a league member, I want to view any contender's winning scenarios, so that I can understand what results would cause them to win.

## Design / Mockup

### Dashboard Insights Panel (Round in Progress)

```
┌─────────────────────────────────────────────────────────────┐
│  📊 Round 23 Insights (5/10 matches played)                 │
│                                                             │
│  ROUND CONTENTION                                           │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Player         │ Current │ Win %  │ Tie %  │ Status    │ │
│  │────────────────│─────────│────────│────────│───────────│ │
│  │ You            │ 18 pts  │ 45%    │ 12%    │ ✓         │ │
│  │ John           │ 22 pts  │ 38%    │ 15%    │ ✓         │ │
│  │ Sarah          │ 19 pts  │ 17%    │ 8%     │ ✓         │ │
│  │ Mike           │ 12 pts  │ 0%     │ 0%     │ Eliminated│ │
│  │ Dave           │ 8 pts   │ 0%     │ 0%     │ Eliminated│ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  [View your winning scenarios]                              │
└─────────────────────────────────────────────────────────────┘
```

### Dashboard Insights Panel (Last Round of Month)

```
┌─────────────────────────────────────────────────────────────┐
│  📊 Round 23 Insights (5/10 matches played)                 │
│  Final round of January                                     │
│                                                             │
│  ROUND CONTENTION                    MONTHLY CONTENTION     │
│  ┌───────────────────────────┐      ┌───────────────────┐   │
│  │ Player    │ Win % │ Tie % │      │ Player │Win%│Tie% │   │
│  │───────────│───────│───────│      │────────│────│─────│   │
│  │ You       │ 45%   │ 12%   │      │ John   │ 52%│ 10% │   │
│  │ John      │ 38%   │ 15%   │      │ You    │ 28%│ 5%  │   │
│  │ Sarah     │ 17%   │ 8%    │      │ Sarah  │ 20%│ 8%  │   │
│  └───────────────────────────┘      └───────────────────┘   │
│                                                             │
│  Combined: 17% chance to win both round AND month           │
│                                                             │
│  [View your winning scenarios]                              │
└─────────────────────────────────────────────────────────────┘
```

### Winning Scenarios View (Click on a Contender)

```
┌─────────────────────────────────────────────────────────────┐
│  Your Winning Scenarios                              [Close] │
│                                                             │
│  You win the round in 1,247 of 2,835 scenarios (44%)        │
│  You win the month in 634 of 2,835 scenarios (22%)          │
│  You win both in 412 of 2,835 scenarios (15%)               │
│                                                             │
│  WHAT YOU NEED:                                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ Liverpool vs Arsenal                                   │ │
│  │ Need: Home win (but NOT 2-1 or 3-2)                    │ │
│  │ Why: John predicted 2-1, Sarah predicted 3-2           │ │
│  ├────────────────────────────────────────────────────────┤ │
│  │ Man City vs Chelsea                                    │ │
│  │ Need: Any result except 1-1                            │ │
│  │ Why: Three players predicted 1-1                       │ │
│  ├────────────────────────────────────────────────────────┤ │
│  │ Wolves vs Brighton                                     │ │
│  │ Any result works ✓                                     │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  [Show all winning scenarios]  [Show scenarios for month]   │
└─────────────────────────────────────────────────────────────┘
```

## Acceptance Criteria

### Core Functionality
- [ ] System correctly identifies eliminated users (those who cannot win even with perfect remaining predictions)
- [ ] System calculates win and tie probabilities for each contender
- [ ] Probabilities update as matches complete during the round
- [ ] Live matches filter out impossible exact scores based on current score
- [ ] Users can view any contender's winning scenarios (not just their own)
- [ ] Monthly insights only shown on the last round of the month

### Elimination Logic
- [ ] A user is eliminated if, when all their remaining predictions are correct, they still don't finish 1st or tied 1st
- [ ] Elimination check accounts for what OTHER users would score given those hypothetical results
- [ ] Boosts are correctly applied (doubled points) for users with active boosts on the round

### Scenario Calculation
- [ ] Scenarios enumerate all unique predictions from contenders plus generic result types
- [ ] Live matches only include achievable exact scores (≥ current score)
- [ ] Generic outcomes (home win/draw/away win not matching any prediction) are included
- [ ] Tied scenarios are tracked separately from outright wins

### Constraint Analysis
- [ ] System identifies what result type(s) each user needs for each remaining match
- [ ] System identifies which specific scorelines are excluded (because other players predicted them)
- [ ] Constraints explain WHY certain scorelines are excluded (who predicted them)

### Display
- [ ] Insights panel shows on league dashboard when a round is in progress
- [ ] Panel shows round contention for any in-progress round
- [ ] Panel shows monthly contention only for the last round of the month
- [ ] Users can click to view detailed winning scenarios
- [ ] Users can click on other contenders to see their scenarios

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Data Models](./01-data-models.md) | Create DTOs for insights, scenarios, and constraints | Not Started |
| 2 | [Scenario Calculator Service](./02-scenario-calculator-service.md) | Core service with elimination and scenario logic | Not Started |
| 3 | [Live Match Filtering](./03-live-match-filtering.md) | Handle in-progress matches and impossible exact scores | Not Started |
| 4 | [Constraint Analyser](./04-constraint-analyser.md) | Analyse winning scenarios to generate human-readable constraints | Not Started |
| 5 | [Query and Handler](./05-query-and-handler.md) | Create GetLeagueInsightsQuery with handler | Not Started |
| 6 | [API Endpoint](./06-api-endpoint.md) | Add insights endpoint to LeaguesController | Not Started |
| 7 | [Dashboard Component](./07-dashboard-component.md) | Create LeagueInsights.razor for basic display | Not Started |
| 8 | [Scenarios Modal](./08-scenarios-modal.md) | Create detailed scenarios view modal | Not Started |

## Dependencies

- [x] League dashboard exists with collapsible sections
- [x] Round status tracking (Draft, Published, InProgress, Completed)
- [x] Match status tracking including live scores
- [x] Predictions stored per user per match
- [x] LeagueRoundResults stores points per user per round
- [x] Boost system tracks which users have boosts applied to rounds
- [ ] Need to verify Match entity has live score properties (LiveHomeScore, LiveAwayScore)

## Technical Notes

### Confirmed Requirements

| Requirement | Decision |
|-------------|----------|
| Tie handling | Split prize - "can win" includes ties for 1st |
| Prize positions | 1st place only (round and monthly) |
| Predictions | All or nothing submission per round |
| Live matches | Filter impossible exact scores; all result types remain possible |
| Concurrent rounds | Only one round in progress at a time |
| Previous rounds | Locked in - only current round's remaining matches affect scenarios |
| Display threshold | Show all mathematically possible users (no minimum %) |

### Scoring System

| Outcome | Points |
|---------|--------|
| Exact score | `League.PointsForExactScore` (typically 5) |
| Correct result | `League.PointsForCorrectResult` (typically 3) |
| Incorrect | 0 |

If user has boost applied to round, all points from that round are doubled.

### Result Type Matching

```csharp
ResultType GetResultType(int homeScore, int awayScore)
{
    if (homeScore > awayScore) return ResultType.HomeWin;
    if (homeScore < awayScore) return ResultType.AwayWin;
    return ResultType.Draw;
}
```

### Scenario Complexity Estimates

| Remaining Matches | Unique Predictions/Match | + Generic | Outcomes/Match | Total Scenarios |
|-------------------|-------------------------|-----------|----------------|-----------------|
| 5 | ~4 | +3 | ~7 | ~16,807 |
| 7 | ~5 | +3 | ~8 | ~2,097,152 |
| 10 | ~6 | +3 | ~9 | ~3.5 billion |

For large scenario counts, consider:
- Only calculating after some matches complete (reducing remaining matches)
- Caching results with short TTL (1-5 minutes)
- Calculating in background and storing results

### Live Match Logic

When a match is in progress with score (currentHome, currentAway):

**Achievable exact scores:** Any (h, a) where h ≥ currentHome AND a ≥ currentAway

**Example:** Match is 2-1 at 75th minute
- Achievable: 2-1, 3-1, 2-2, 3-2, 4-1, 2-3, etc.
- Impossible: 2-0, 1-0, 1-1, 0-0, 0-1, etc.

A user who predicted 2-0 can no longer get exact score points (max 3 pts for correct result if final is a home win).

### Elimination Logic (Critical)

For each user U, to check if eliminated:
1. Assume U's remaining predictions are the actual results
2. Calculate U's final points (all exact scores, possibly boosted)
3. Calculate EVERYONE ELSE's final points given those same results
4. If U does not finish 1st or tied 1st → U is ELIMINATED

This is O(n) scenarios for n users in the elimination phase.

### Data Access Pattern

Follow existing CQRS patterns:
- Query handlers use `IApplicationReadDbConnection` with custom SQL
- No repositories in query handlers
- Return DTOs, not domain models

## Open Questions

- [x] Should "0 wins" be shown or hidden? → **Show all mathematically possible**
- [x] Tie-breaking rules? → **Ties split the prize, no tiebreaker**
- [x] What prize positions matter? → **1st place only**
- [x] How to handle live matches? → **Filter impossible exact scores**
- [ ] Should insights be cached? If so, for how long?
- [ ] Should we limit scenario calculation to rounds with ≤ N remaining matches?
- [ ] Performance testing needed - what's acceptable response time?

## Future Enhancements (Out of Scope)

- Email notifications ("The race is heating up!")
- Push notifications when user's status changes (eliminated, back in contention)
- Historical "what could have been" view after round completes
- Probability charts/visualisations
- "Key match" identification (which match affects you most)
