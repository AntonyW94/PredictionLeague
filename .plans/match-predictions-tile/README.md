# Plan: Match Predictions on Upcoming Rounds Tile

## Overview

Add a compact view of matches and user predictions to the "Upcoming Rounds" tile on the dashboard. Each match displays the home team logo, predicted home score (or dash), "v", predicted away score (or dash), and away team logo.

## Visual Reference

**Current State:**
```
┌─────────────────────────────┐
│     Premier League 2025/26  │
│          Round 22           │
│     ⏰ 17 Jan 2026 12:00    │
│    [ Edit Predictions ]     │
└─────────────────────────────┘
```

**Target State:**
```
┌─────────────────────────────┐
│     Premier League 2025/26  │
│          Round 22           │
│     ⏰ 17 Jan 2026 12:00    │
│                             │
│  🔴 0 v 2 🔵  🦁 2 v 1 ⚽   │  ← Desktop: 2 columns
│  🟡 - v - 🔴  🐝 1 v 1 🦊   │
│  ⬜ 3 v 1 🟣  🐓 - v - 🔵   │
│  🟠 2 v 2 ⚫  🦅 1 v 0 🐺   │
│  🔴 1 v 1 🟢  ⬛ - v - 🟤   │
│                             │
│    [ Edit Predictions ]     │
└─────────────────────────────┘
```

**Mobile: Single column layout**

## Requirements

1. Display all matches for each round in the carousel
2. Show team logos (20px) with SVG placeholder for loading/errors
3. Show predicted scores or "-" if no prediction exists
4. Desktop: 2-column grid layout
5. Mobile (≤768px): Single column layout
6. Matches ordered by kickoff time (earliest first), then by home team short name
7. All rounds in the carousel must show their matches

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         BACKEND                                  │
├─────────────────────────────────────────────────────────────────┤
│  Contracts Layer                                                 │
│  ├── UpcomingMatchDto.cs (NEW)                                  │
│  └── UpcomingRoundDto.cs (MODIFY - add Matches property)        │
├─────────────────────────────────────────────────────────────────┤
│  Application Layer                                               │
│  └── GetUpcomingRoundsQueryHandler.cs (MODIFY - fetch matches)  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                         FRONTEND                                 │
├─────────────────────────────────────────────────────────────────┤
│  Static Assets                                                   │
│  └── wwwroot/images/team-placeholder.svg (NEW)                  │
├─────────────────────────────────────────────────────────────────┤
│  Components                                                      │
│  └── Pages/Dashboard/RoundCard.razor (MODIFY - add match grid)  │
│  └── Pages/Dashboard/RoundCard.razor.css (MODIFY - add styles)  │
└─────────────────────────────────────────────────────────────────┘
```

## Task Breakdown

Complete these tasks in order:

| # | Task | File |
|---|------|------|
| 1 | [Create UpcomingMatchDto](./01-create-upcoming-match-dto.md) | `Contracts/Dashboard/UpcomingMatchDto.cs` |
| 2 | [Extend UpcomingRoundDto](./02-extend-upcoming-round-dto.md) | `Contracts/Dashboard/UpcomingRoundDto.cs` |
| 3 | [Update Query Handler](./03-update-query-handler.md) | `Application/Features/Dashboard/Queries/GetUpcomingRoundsQueryHandler.cs` |
| 4 | [Create Placeholder SVG](./04-create-placeholder-svg.md) | `Web.Client/wwwroot/images/team-placeholder.svg` |
| 5 | [Update RoundCard Component](./05-update-round-card-component.md) | `Web.Client/Components/Pages/Dashboard/RoundCard.razor` |
| 6 | [Add Responsive Styles](./06-add-styles.md) | `Web.Client/Components/Pages/Dashboard/RoundCard.razor.css` |
| 7 | [Testing Checklist](./07-testing-checklist.md) | Manual verification |

## Database Schema Reference

### Relevant Tables

```
[Rounds]
├── Id (int, PK)
├── SeasonId (int, FK)
├── RoundNumber (int)
├── DeadlineUtc (datetime)
└── Status (int)

[Matches]
├── Id (int, PK)
├── RoundId (int, FK)
├── HomeTeamId (int, FK)
├── AwayTeamId (int, FK)
├── MatchDateTimeUtc (datetime)
├── HomeScore (int?)
└── AwayScore (int?)

[Teams]
├── Id (int, PK)
├── Name (string)
├── ShortName (string)
├── Abbreviation (string)
└── LogoUrl (string?)

[UserPredictions]
├── Id (int, PK)
├── UserId (string, FK)
├── MatchId (int, FK)
├── HomeScore (int)
└── AwayScore (int)
```

## Key Patterns to Follow

### CQRS Query Pattern
- Query handlers use `IApplicationReadDbConnection` directly
- Write raw SQL with Dapper
- Return DTOs, not domain models

### DateTime Handling
- All dates stored in UTC
- Property names use `Utc` suffix

### Existing Code References
- Similar query pattern: `GetPredictionPageDataQueryHandler.cs`
- Similar DTO: `MatchPredictionDto.cs`
- Team logo usage: `Predictions.razor`

## Constraints

- Maximum 5 rounds in carousel
- Typically 10 matches per round (can vary)
- Team logos come from external API (api-sports.io)
- Must handle null/missing/broken logo URLs gracefully
