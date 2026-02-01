# Tournament Support Feature Plan

## Overview

This document outlines a comprehensive plan to extend PredictionLeague to support tournament-format competitions (e.g., FIFA World Cup 2026, UEFA Champions League knockout stages) alongside the existing league format (e.g., Premier League).

**Target Delivery:** May 2026 (for FIFA World Cup 2026 starting June 2026)

---

## Table of Contents

1. [Current State Analysis](#1-current-state-analysis)
2. [Tournament Requirements](#2-tournament-requirements)
3. [API-Football Integration Research](#3-api-football-integration-research)
4. [Architecture Design](#4-architecture-design)
5. [Database Schema Changes](#5-database-schema-changes)
6. [Domain Model Changes](#6-domain-model-changes)
7. [Application Layer Changes](#7-application-layer-changes)
8. [API Endpoint Changes](#8-api-endpoint-changes)
9. [UI/UX Changes](#9-uiux-changes)
10. [Implementation Phases](#10-implementation-phases)
11. [Risk Analysis](#11-risk-analysis)
12. [Testing Strategy](#12-testing-strategy)

---

## 1. Current State Analysis

### Existing Architecture Strengths

The current codebase has several features that support tournament implementation:

| Feature | Status | Notes |
|---------|--------|-------|
| `Match.CustomLockTimeUtc` | Column exists, unused | Per-match deadline support ready |
| `Match.PlaceholderHomeName/AwayName` | Column exists, unused | "Winner Group A" style placeholders ready |
| `Match.HomeTeamId/AwayTeamId` nullable | ✅ Implemented | Supports TBD teams |
| `Seasons.CompetitionType` | In DB schema, not in domain | Needs domain model update |
| Prize strategies | Extensible pattern | Can add tournament-specific strategies |
| Configurable scoring | ✅ Per league | `PointsForExactScore`, `PointsForCorrectResult` |

### Current Limitations

| Limitation | Impact | Solution Required |
|------------|--------|-------------------|
| Single deadline per round | High | Enable `CustomLockTimeUtc` usage |
| No concept of tournament stages | High | New `TournamentStage` entity |
| No group concept | Medium | New grouping mechanism |
| Round assumes ≥10 matches | Medium | Flexible match grouping |
| Linear round progression | Medium | Stage-based progression |
| API sync assumes league format | High | Update sync logic |

### Current Data Flow

```
Season (1) → Round (many) → Match (many) → UserPrediction (per user)
   ↓
League (many) → LeagueMember → LeagueRoundResult (scoring cache)
```

**Key Observation:** The Round entity is currently tightly coupled to the league format assumption of 10 matches per gameweek with a single deadline.

---

## 2. Tournament Requirements

### 2.1 Core Requirements

1. **Per-match deadlines** - Each match can have its own prediction lock time
2. **Placeholder teams** - Support "Winner Group A" until teams are determined
3. **Minimum 4 matches per prediction round** - User requirement for knockout stages
4. **Stage awareness** - Group Stage, Round of 32, Round of 16, Quarter-finals, Semi-finals, Final
5. **Group identifiers** - Track which group each match belongs to (Group A, B, C, etc.)

### 2.2 Competition Types to Support

| Competition Type | Format | Example |
|------------------|--------|---------|
| **League** | Linear rounds, same teams | Premier League |
| **Tournament** | Groups → Knockout | FIFA World Cup |
| **Hybrid** | League phase → Knockout | UEFA Champions League (2024+ format) |

### 2.3 FIFA World Cup 2026 Specifics

- **48 teams** in 12 groups of 4
- **Group Stage:** 3 matchdays per group = 36 matchdays total (but can group into prediction rounds)
- **Round of 32:** 16 matches (can be one prediction round)
- **Round of 16:** 8 matches (need to combine with other matches for 4+ minimum)
- **Quarter-finals:** 4 matches (need to combine)
- **Semi-finals:** 2 matches (need to combine)
- **Third-place play-off + Final:** 2 matches (combine as "Finals" round)

### 2.4 Champions League Specifics (Hybrid Format)

- **League Phase:** 36 teams, each plays 8 matches (similar to current league format)
- **Knockout Phase Play-offs:** 16 teams (8 matches)
- **Round of 16:** 8 matches (two-legged)
- **Quarter-finals → Final:** Standard knockout

---

## 3. API-Football Integration Research

### 3.1 API Response Structure

API-Football (api-sports.io) uses the same endpoint structure for both leagues and tournaments. The key differences are in the `round` field naming:

**League Format (Premier League):**
```json
{
  "fixture": { "id": 123, "date": "2025-12-14T15:00:00+00:00" },
  "league": {
    "id": 39,
    "round": "Regular Season - 16"
  }
}
```

**Tournament Format (World Cup):**
```json
{
  "fixture": { "id": 456, "date": "2026-06-14T19:00:00+00:00" },
  "league": {
    "id": 1,
    "round": "Group A - 1"  // Group stage
  }
}
```

```json
{
  "fixture": { "id": 789, "date": "2026-07-05T20:00:00+00:00" },
  "league": {
    "id": 1,
    "round": "Round of 16"  // Knockout stage
  }
}
```

### 3.2 Round Name Patterns

| Stage | API Round Name Pattern | Example |
|-------|----------------------|---------|
| League | `Regular Season - {N}` | `Regular Season - 16` |
| Group Stage | `Group {Letter} - {Matchday}` | `Group A - 1`, `Group B - 2` |
| Round of 32 | `Round of 32` | Single value |
| Round of 16 | `Round of 16` | Single value |
| Quarter-finals | `Quarter-finals` | Single value |
| Semi-finals | `Semi-finals` | Single value |
| Third-place | `3rd Place Final` | Single value |
| Final | `Final` | Single value |

### 3.3 Key Integration Changes

1. **Round Name Parsing** - Update `SyncSeasonWithApiCommand` to handle tournament round names
2. **Stage Detection** - Parse round name to determine stage type
3. **Group Extraction** - Parse "Group A - 1" to extract group letter
4. **Flexible Round Creation** - Allow system to group API rounds into prediction rounds

### 3.4 API Coverage Confirmation

- FIFA World Cup: League ID `1` (World Cup 2022 available, 2026 will follow same structure)
- UEFA Champions League: League ID `2`
- UEFA Europa League: League ID `3`

Sources:
- [API-Football Documentation](https://www.api-football.com/documentation-v3)
- [API-Sports Documentation](https://api-sports.io/documentation/football/v3)

---

## 4. Architecture Design

### 4.1 Conceptual Model

```
Season
  ├── CompetitionType (League | Tournament | Hybrid)
  │
  ├── [For Tournaments] TournamentStages
  │     ├── GroupStage (Groups A-L, each with Matchdays 1-3)
  │     ├── RoundOf32
  │     ├── RoundOf16
  │     ├── QuarterFinals
  │     ├── SemiFinals
  │     └── Final
  │
  └── Rounds (Prediction Rounds - what users see)
        ├── Round 1 ("Group Stage - Matchday 1")
        │     └── Matches from Group A-L, Matchday 1
        ├── Round 2 ("Group Stage - Matchday 2")
        ├── Round 3 ("Group Stage - Matchday 3")
        ├── Round 4 ("Round of 32")
        ├── Round 5 ("Round of 16")
        ├── Round 6 ("Quarter-finals & Semi-finals") ← Combined!
        └── Round 7 ("Finals")
```

### 4.2 Key Design Decisions

#### Decision 1: Reuse Round Entity with Extensions

**Rationale:** The existing `Round` entity works well for grouping matches. We extend it rather than create a parallel system.

**Changes:**
- Add `TournamentStageId` (nullable FK) to Round
- Add `ApiRoundNames` (string, comma-separated) to map multiple API rounds to one prediction round

#### Decision 2: Per-Match Deadlines via CustomLockTimeUtc

**Rationale:** Column already exists. Enable its use in deadline enforcement logic.

**Changes:**
- Update `PredictionDomainService` to check match-level deadlines
- Update UI to show per-match lock status
- Update API to return per-match lock times

#### Decision 3: Tournament Stage as Lookup/Configuration

**Rationale:** Stages are predictable (Group, RO32, RO16, QF, SF, F). Store as configuration rather than dynamic entities.

**Implementation:**
- `TournamentStage` table with predefined stages
- Each stage has: `Code`, `Name`, `DisplayOrder`, `MinMatchesRequired`

#### Decision 4: Flexible Round Grouping

**Rationale:** Admin can decide how to group tournament matches into prediction rounds.

**Implementation:**
- Admin UI allows selecting multiple API rounds when creating a prediction round
- System calculates deadline as earliest match time minus buffer

---

## 5. Database Schema Changes

### 5.1 New Tables

#### TournamentStages (Lookup Table)

```sql
CREATE TABLE [TournamentStages] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Code] NVARCHAR(50) NOT NULL UNIQUE,
    [Name] NVARCHAR(100) NOT NULL,
    [DisplayOrder] INT NOT NULL,
    [StageType] NVARCHAR(20) NOT NULL, -- 'Group', 'Knockout'
    [MinMatchesForRound] INT NOT NULL DEFAULT 4
);

-- Seed data
INSERT INTO [TournamentStages] ([Code], [Name], [DisplayOrder], [StageType], [MinMatchesForRound])
VALUES
    ('GROUP', 'Group Stage', 1, 'Group', 4),
    ('ROUND_OF_32', 'Round of 32', 2, 'Knockout', 4),
    ('ROUND_OF_16', 'Round of 16', 3, 'Knockout', 4),
    ('QUARTER_FINALS', 'Quarter-finals', 4, 'Knockout', 4),
    ('SEMI_FINALS', 'Semi-finals', 5, 'Knockout', 2),
    ('THIRD_PLACE', 'Third-place Play-off', 6, 'Knockout', 1),
    ('FINAL', 'Final', 7, 'Knockout', 1);
```

#### TournamentGroups (For Group Stage)

```sql
CREATE TABLE [TournamentGroups] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [SeasonId] INT NOT NULL FOREIGN KEY REFERENCES [Seasons]([Id]) ON DELETE CASCADE,
    [GroupLetter] CHAR(1) NOT NULL, -- A, B, C, etc.
    [Name] NVARCHAR(50) NOT NULL, -- "Group A"
    CONSTRAINT UQ_TournamentGroups_SeasonGroup UNIQUE ([SeasonId], [GroupLetter])
);
```

### 5.2 Modified Tables

#### Seasons - Add CompetitionType to Domain

```sql
-- Column already exists in schema, ensure domain model uses it
-- CompetitionType: 0 = League, 1 = Tournament, 2 = Hybrid
ALTER TABLE [Seasons] ADD CONSTRAINT DF_Seasons_CompetitionType DEFAULT 0 FOR [CompetitionType];
```

#### Rounds - Add Tournament Support

```sql
ALTER TABLE [Rounds] ADD [TournamentStageId] INT NULL
    FOREIGN KEY REFERENCES [TournamentStages]([Id]);

ALTER TABLE [Rounds] ADD [ApiRoundNames] NVARCHAR(500) NULL;
-- Stores comma-separated API round names when one prediction round spans multiple API rounds
-- e.g., "Quarter-finals,Semi-finals" for a combined knockout round

ALTER TABLE [Rounds] ADD [DisplayName] NVARCHAR(100) NULL;
-- e.g., "Quarter-finals & Semi-finals" - allows custom naming
```

#### Matches - Add Group Reference

```sql
ALTER TABLE [Matches] ADD [TournamentGroupId] INT NULL
    FOREIGN KEY REFERENCES [TournamentGroups]([Id]);

ALTER TABLE [Matches] ADD [ApiRoundName] NVARCHAR(128) NULL;
-- Stores the original API round name for this specific match
-- e.g., "Group A - 2" or "Quarter-finals"
```

### 5.3 Schema Diagram (Tournament Mode)

```
┌─────────────┐     ┌──────────────────┐     ┌─────────────┐
│   Seasons   │────<│ TournamentGroups │     │TournamentStg│
│CompetitionTy│     │   (Group A-L)    │     │  (Lookup)   │
└─────────────┘     └──────────────────┘     └─────────────┘
       │                     │                      │
       │                     ▼                      │
       ▼              ┌─────────────┐               │
┌─────────────┐       │   Matches   │               │
│   Rounds    │──────<│TournamentGrp│               │
│TournamentStg│───────│CustomLockUtc│               │
│ApiRoundNames│       │ApiRoundName │               │
└─────────────┘       └─────────────┘               │
                             │                      │
                             ▼                      │
                      ┌─────────────┐               │
                      │UserPredictn │               │
                      └─────────────┘               │
```

---

## 6. Domain Model Changes

### 6.1 New Enumerations

**File:** `PredictionLeague.Domain/Common/Enumerations/CompetitionType.cs`

```csharp
namespace PredictionLeague.Domain.Common.Enumerations;

public enum CompetitionType
{
    League = 0,
    Tournament = 1,
    Hybrid = 2
}
```

**File:** `PredictionLeague.Domain/Common/Enumerations/TournamentStageType.cs`

```csharp
namespace PredictionLeague.Domain.Common.Enumerations;

public enum TournamentStageType
{
    Group,
    Knockout
}
```

### 6.2 New Domain Models

**File:** `PredictionLeague.Domain/Models/TournamentStage.cs`

```csharp
namespace PredictionLeague.Domain.Models;

public class TournamentStage
{
    public int Id { get; init; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public TournamentStageType StageType { get; private set; }
    public int MinMatchesForRound { get; private set; }

    // Constructor for hydration from database
    public TournamentStage(int id, string code, string name, int displayOrder,
        TournamentStageType stageType, int minMatchesForRound)
    {
        Id = id;
        Code = code;
        Name = name;
        DisplayOrder = displayOrder;
        StageType = stageType;
        MinMatchesForRound = minMatchesForRound;
    }
}
```

**File:** `PredictionLeague.Domain/Models/TournamentGroup.cs`

```csharp
namespace PredictionLeague.Domain.Models;

public class TournamentGroup
{
    public int Id { get; init; }
    public int SeasonId { get; private set; }
    public char GroupLetter { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private TournamentGroup() { }

    public TournamentGroup(int id, int seasonId, char groupLetter, string name)
    {
        Id = id;
        SeasonId = seasonId;
        GroupLetter = groupLetter;
        Name = name;
    }

    public static TournamentGroup Create(int seasonId, char groupLetter)
    {
        Guard.Against.Default(seasonId);
        Guard.Against.OutOfRange(groupLetter, nameof(groupLetter), 'A', 'Z');

        return new TournamentGroup
        {
            SeasonId = seasonId,
            GroupLetter = groupLetter,
            Name = $"Group {groupLetter}"
        };
    }
}
```

### 6.3 Modified Domain Models

**File:** `PredictionLeague.Domain/Models/Season.cs` (additions)

```csharp
// Add property
public CompetitionType CompetitionType { get; private set; }

// Update constructor to include CompetitionType
public Season(int id, string name, DateTime startDateUtc, DateTime endDateUtc,
    bool isActive, int numberOfRounds, int? apiLeagueId, CompetitionType competitionType)
{
    // ... existing assignments ...
    CompetitionType = competitionType;
}

// Update Create factory method
public static Season Create(string name, DateTime startDateUtc, DateTime endDateUtc,
    bool isActive, int numberOfRounds, int? apiLeagueId, CompetitionType competitionType)
{
    // ... validation ...
    return new Season
    {
        // ... existing properties ...
        CompetitionType = competitionType
    };
}

// Helper methods
public bool IsTournament => CompetitionType == CompetitionType.Tournament
    || CompetitionType == CompetitionType.Hybrid;

public bool HasGroupStage => CompetitionType == CompetitionType.Tournament;
```

**File:** `PredictionLeague.Domain/Models/Round.cs` (additions)

```csharp
// Add properties
public int? TournamentStageId { get; private set; }
public string? ApiRoundNames { get; private set; } // Comma-separated
public string? DisplayName { get; private set; }

// Add method to get list of API round names
public IEnumerable<string> GetApiRoundNamesList()
{
    if (string.IsNullOrEmpty(ApiRoundNames))
        return ApiRoundName != null ? new[] { ApiRoundName } : Enumerable.Empty<string>();

    return ApiRoundNames.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(n => n.Trim());
}

// Update constructor and Create method to support tournament properties
```

**File:** `PredictionLeague.Domain/Models/Match.cs` (additions)

```csharp
// Add properties
public int? TournamentGroupId { get; private set; }
public string? ApiRoundName { get; private set; }

// Add method for effective deadline
public DateTime GetEffectiveDeadline(DateTime roundDeadline)
{
    return CustomLockTimeUtc ?? roundDeadline;
}

// Add method to check if predictions are locked
public bool IsPredictionLocked(DateTime utcNow, DateTime roundDeadline)
{
    var effectiveDeadline = GetEffectiveDeadline(roundDeadline);
    return utcNow >= effectiveDeadline;
}

// Factory method for tournament matches with placeholders
public static Match CreateWithPlaceholders(int roundId, DateTime matchDateTimeUtc,
    int? externalId, string placeholderHomeName, string placeholderAwayName,
    DateTime customLockTimeUtc, int? tournamentGroupId, string? apiRoundName)
{
    return new Match
    {
        RoundId = roundId,
        HomeTeamId = null,
        AwayTeamId = null,
        PlaceholderHomeName = placeholderHomeName,
        PlaceholderAwayName = placeholderAwayName,
        MatchDateTimeUtc = matchDateTimeUtc,
        CustomLockTimeUtc = customLockTimeUtc,
        Status = MatchStatus.Scheduled,
        ExternalId = externalId,
        TournamentGroupId = tournamentGroupId,
        ApiRoundName = apiRoundName
    };
}

// Method to update team when determined
public void AssignTeams(int homeTeamId, int awayTeamId)
{
    Guard.Against.Expression(h => h == awayTeamId, homeTeamId, "A team cannot play against itself.");

    HomeTeamId = homeTeamId;
    AwayTeamId = awayTeamId;
    PlaceholderHomeName = null;
    PlaceholderAwayName = null;
}
```

### 6.4 Updated Domain Service

**File:** `PredictionLeague.Domain/Services/PredictionDomainService.cs`

```csharp
public IEnumerable<UserPrediction> SubmitPredictions(
    Round round,
    string userId,
    IEnumerable<(int MatchId, int HomeScore, int AwayScore)> predictedScores,
    bool enforcePerMatchDeadlines = false) // New parameter
{
    Guard.Against.Null(round);

    var now = DateTime.UtcNow;
    var predictions = new List<UserPrediction>();

    foreach (var prediction in predictedScores)
    {
        var match = round.Matches.FirstOrDefault(m => m.Id == prediction.MatchId);
        if (match == null)
            throw new InvalidOperationException($"Match (ID: {prediction.MatchId}) not found in round.");

        // Check per-match deadline if enabled, otherwise use round deadline
        if (enforcePerMatchDeadlines)
        {
            if (match.IsPredictionLocked(now, round.DeadlineUtc))
                throw new InvalidOperationException(
                    $"The deadline for match (ID: {prediction.MatchId}) has passed.");
        }
        else if (round.DeadlineUtc < now)
        {
            throw new InvalidOperationException(
                "The deadline for submitting predictions for this round has passed.");
        }

        predictions.Add(UserPrediction.Create(userId, prediction.MatchId,
            prediction.HomeScore, prediction.AwayScore));
    }

    return predictions;
}
```

---

## 7. Application Layer Changes

### 7.1 New Repositories

**File:** `PredictionLeague.Application/Repositories/ITournamentStageRepository.cs`

```csharp
public interface ITournamentStageRepository
{
    Task<IEnumerable<TournamentStage>> GetAllAsync(CancellationToken cancellationToken);
    Task<TournamentStage?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}
```

**File:** `PredictionLeague.Application/Repositories/ITournamentGroupRepository.cs`

```csharp
public interface ITournamentGroupRepository
{
    Task<IEnumerable<TournamentGroup>> GetBySeasonIdAsync(int seasonId, CancellationToken cancellationToken);
    Task<TournamentGroup?> GetBySeasonAndLetterAsync(int seasonId, char groupLetter, CancellationToken cancellationToken);
    Task<TournamentGroup> CreateAsync(TournamentGroup group, CancellationToken cancellationToken);
}
```

### 7.2 Updated API Sync Logic

**File:** `PredictionLeague.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs`

Key changes:
1. Parse tournament round names (e.g., "Group A - 1", "Quarter-finals")
2. Create `TournamentGroup` records for group stage matches
3. Set `CustomLockTimeUtc` on matches (30 minutes before kick-off)
4. Set `ApiRoundName` on individual matches
5. Handle placeholder teams for knockout matches

```csharp
// Add helper method to parse tournament round names
private static (TournamentStageCode Stage, char? GroupLetter, int? Matchday) ParseTournamentRoundName(string roundName)
{
    // "Group A - 1" → (Group, 'A', 1)
    if (roundName.StartsWith("Group "))
    {
        var parts = roundName.Split(" - ");
        var groupLetter = parts[0].Last();
        var matchday = int.TryParse(parts.LastOrDefault(), out var md) ? md : (int?)null;
        return (TournamentStageCode.GROUP, groupLetter, matchday);
    }

    // "Round of 32" → (RoundOf32, null, null)
    // "Quarter-finals" → (QuarterFinals, null, null)
    // etc.
    return roundName switch
    {
        "Round of 32" => (TournamentStageCode.ROUND_OF_32, null, null),
        "Round of 16" => (TournamentStageCode.ROUND_OF_16, null, null),
        "Quarter-finals" => (TournamentStageCode.QUARTER_FINALS, null, null),
        "Semi-finals" => (TournamentStageCode.SEMI_FINALS, null, null),
        "3rd Place Final" => (TournamentStageCode.THIRD_PLACE, null, null),
        "Final" => (TournamentStageCode.FINAL, null, null),
        _ => throw new InvalidOperationException($"Unknown tournament round: {roundName}")
    };
}
```

### 7.3 New Commands

**File:** `PredictionLeague.Application/Features/Admin/Rounds/Commands/CreateTournamentRoundCommand.cs`

```csharp
public record CreateTournamentRoundCommand(
    int SeasonId,
    int RoundNumber,
    string DisplayName,
    List<string> ApiRoundNames, // e.g., ["Quarter-finals", "Semi-finals"]
    int? TournamentStageId,
    DateTime? OverrideDeadlineUtc
) : IRequest<int>;
```

### 7.4 Updated Queries

**File:** `PredictionLeague.Application/Features/Predictions/Queries/GetPredictionPageDataQueryHandler.cs`

Add per-match deadline information:

```csharp
// Add to SQL query
m.[CustomLockTimeUtc],
m.[PlaceholderHomeName],
m.[PlaceholderAwayName],

// Add to DTO mapping
IsLocked = match.CustomLockTimeUtc.HasValue
    ? match.CustomLockTimeUtc.Value < DateTime.UtcNow
    : roundDeadline < DateTime.UtcNow,
EffectiveDeadlineUtc = match.CustomLockTimeUtc ?? roundDeadline
```

---

## 8. API Endpoint Changes

### 8.1 Updated DTOs

**File:** `PredictionLeague.Contracts/Dashboard/MatchPredictionDto.cs`

```csharp
public class MatchPredictionDto
{
    // ... existing properties ...

    // New properties for tournament support
    public DateTime? CustomLockTimeUtc { get; init; }
    public DateTime EffectiveDeadlineUtc { get; init; }
    public bool IsLocked { get; init; }
    public string? PlaceholderHomeName { get; init; }
    public string? PlaceholderAwayName { get; init; }
    public string? GroupName { get; init; } // e.g., "Group A"
    public string? StageName { get; init; } // e.g., "Quarter-finals"
}
```

**File:** `PredictionLeague.Contracts/Predictions/PredictionPageDto.cs`

```csharp
public class PredictionPageDto
{
    // ... existing properties ...

    // New properties
    public bool HasPerMatchDeadlines { get; init; }
    public string? TournamentStageName { get; init; }
    public List<MatchGroupDto> MatchGroups { get; init; } = []; // For grouped display
}

public class MatchGroupDto
{
    public string GroupName { get; init; } = string.Empty; // e.g., "Group A" or "Quarter-finals"
    public List<MatchPredictionDto> Matches { get; init; } = [];
}
```

### 8.2 New Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/admin/seasons/{id}/groups` | GET | Get tournament groups for a season |
| `/api/admin/seasons/{id}/groups` | POST | Create tournament groups |
| `/api/admin/rounds/tournament` | POST | Create tournament round with multiple API rounds |
| `/api/seasons/{id}/stage/{stageId}/matches` | GET | Get matches for a specific stage |
| `/api/tournament-stages` | GET | Get all tournament stage definitions |

---

## 9. UI/UX Changes

### 9.1 Prediction Page Updates

**Per-Match Lock Indicators:**

```html
<!-- For each match card, show lock status -->
@if (match.IsLocked)
{
    <div class="match-locked-badge">
        <i class="bi bi-lock-fill"></i> Locked
    </div>
}
else if (match.CustomLockTimeUtc.HasValue)
{
    <div class="match-deadline-badge">
        <i class="bi bi-clock"></i>
        Locks: <LocalTime UtcDate="@match.EffectiveDeadlineUtc" />
    </div>
}
```

**Grouped Display for Tournaments:**

```html
@if (_pageData.HasPerMatchDeadlines)
{
    @foreach (var group in _pageData.MatchGroups)
    {
        <div class="match-group">
            <h4 class="group-header">@group.GroupName</h4>
            @foreach (var match in group.Matches)
            {
                <PredictionCard Match="@match" />
            }
        </div>
    }
}
else
{
    <!-- Current single-deadline display -->
}
```

**Placeholder Team Display:**

```html
@if (!string.IsNullOrEmpty(match.PlaceholderHomeName))
{
    <div class="team-display placeholder">
        <span class="placeholder-badge">TBD</span>
        <span class="team-name">@match.PlaceholderHomeName</span>
    </div>
}
else
{
    <!-- Normal team display with logo -->
}
```

### 9.2 Admin Round Creation (Tournament Mode)

New admin page or modal for creating tournament rounds:

1. Select tournament stage (dropdown)
2. Multi-select API rounds to include (checkbox list)
3. Preview matches that will be included
4. Set optional deadline override
5. Set display name

### 9.3 Dashboard Changes

- Show tournament stage badges on round cards
- Display group letters for group stage matches
- Show "TBD" for placeholder teams in upcoming rounds

---

## 10. Implementation Phases

### Phase 1: Foundation (2 weeks)

**Tasks:**
1. Add `CompetitionType` to Season domain model
2. Create `TournamentStage` lookup table and repository
3. Create `TournamentGroup` entity and repository
4. Add new columns to `Rounds` table (`TournamentStageId`, `ApiRoundNames`, `DisplayName`)
5. Add new columns to `Matches` table (`TournamentGroupId`, `ApiRoundName`)
6. Create database migration scripts
7. Update Season Create/Update commands and DTOs

**Deliverables:**
- Database schema updated
- Domain models extended
- Season admin pages show competition type selector

### Phase 2: Per-Match Deadlines (1 week)

**Tasks:**
1. Enable `CustomLockTimeUtc` in prediction submission logic
2. Update `PredictionDomainService` to check per-match deadlines
3. Update `GetPredictionPageDataQueryHandler` to return match-level deadlines
4. Update `MatchPredictionDto` with lock status
5. Update Predictions.razor to show per-match lock status

**Deliverables:**
- Per-match deadline enforcement working
- UI shows individual match lock status

### Phase 3: Tournament Round Creation (2 weeks)

**Tasks:**
1. Create `CreateTournamentRoundCommand` and handler
2. Update API sync logic to handle tournament round names
3. Parse group stage round names ("Group A - 1")
4. Parse knockout round names ("Quarter-finals")
5. Auto-create `TournamentGroup` records during sync
6. Set `CustomLockTimeUtc` on all tournament matches
7. Create admin UI for tournament round creation

**Deliverables:**
- Tournament seasons can be synced from API
- Admin can create grouped prediction rounds
- Matches correctly assigned to groups/stages

### Phase 4: Placeholder Teams (1 week)

**Tasks:**
1. Update Match factory method for placeholder teams
2. Update API sync to create placeholder matches for knockout stages
3. Create command to assign teams when determined
4. Update UI to display placeholder team names
5. Prevent predictions on matches with placeholder teams (optional)

**Deliverables:**
- Knockout matches show "Winner Group A vs Runner-up Group B"
- Teams can be assigned when known

### Phase 5: UI Polish & Testing (2 weeks)

**Tasks:**
1. Grouped match display on prediction page
2. Tournament stage badges/headers
3. Mobile-responsive tournament UI
4. Integration testing with World Cup 2022 data (test API data)
5. End-to-end user flow testing
6. Admin workflow testing
7. Performance testing with 48+ teams

**Deliverables:**
- Full tournament prediction flow working
- Admin can manage tournament seasons
- Mobile-friendly UI

### Phase 6: Champions League Support (1 week)

**Tasks:**
1. Test hybrid competition type (league phase + knockout)
2. Verify transition from league phase to knockout phase
3. Ensure scoring/leaderboards work across competition types

**Deliverables:**
- Champions League 2025/26 can be supported

---

## 11. Risk Analysis

### High Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| API-Football 2026 World Cup data not available in time | Critical | Test with 2022 World Cup data; have manual entry fallback |
| Complex deadline logic causes bugs | High | Extensive unit testing; gradual rollout with feature flag |
| Performance with 48 teams × 64 matches | Medium | Index optimisation; pagination |

### Medium Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| User confusion with per-match deadlines | Medium | Clear UI with countdown timers per match |
| Admin complexity in round creation | Medium | Good defaults; preview functionality |
| Database migration issues | Medium | Thorough testing in staging environment |

### Low Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Placeholder team display confusion | Low | Clear "TBD" badges with explanatory text |
| Group stage sorting complexity | Low | Default sort by group letter, then matchday |

---

## 12. Testing Strategy

### Unit Tests

- `PredictionDomainService.SubmitPredictions` with per-match deadlines
- `Match.IsPredictionLocked` with various deadline scenarios
- `Match.GetEffectiveDeadline` logic
- Tournament round name parsing
- Group letter extraction

### Integration Tests

- Create tournament season → sync from API → create rounds
- Submit predictions with mixed locked/unlocked matches
- Leaderboard calculation for tournament leagues

### End-to-End Tests

1. **Admin Flow:**
   - Create tournament season
   - Sync from API
   - Review auto-created groups
   - Create prediction rounds
   - Enter results

2. **User Flow:**
   - Join tournament league
   - View grouped predictions page
   - Submit predictions before individual deadlines
   - View locked matches after deadline
   - View leaderboard

### Test Data

Use FIFA World Cup 2022 (Qatar) data from API-Football as test data:
- League ID: 1
- Season: 2022
- Full tournament structure available

---

## Appendix A: API Round Name Examples

### FIFA World Cup

```
Group A - 1
Group A - 2
Group A - 3
Group B - 1
...
Group L - 3
Round of 32
Round of 16
Quarter-finals
Semi-finals
3rd Place Final
Final
```

### UEFA Champions League (2024+ format)

```
League Phase - 1
League Phase - 2
...
League Phase - 8
Knockout Phase Play-offs
Round of 16
Quarter-finals
Semi-finals
Final
```

### UEFA Europa League

```
League Phase - 1
...
Knockout Phase Play-offs
Round of 16
Quarter-finals
Semi-finals
Final
```

---

## Appendix B: Sample Database Queries

### Get Tournament Matches with Group Info

```sql
SELECT
    m.[Id],
    m.[MatchDateTimeUtc],
    m.[CustomLockTimeUtc],
    COALESCE(ht.[Name], m.[PlaceholderHomeName]) AS HomeTeam,
    COALESCE(at.[Name], m.[PlaceholderAwayName]) AS AwayTeam,
    tg.[Name] AS GroupName,
    ts.[Name] AS StageName
FROM [Matches] m
JOIN [Rounds] r ON m.[RoundId] = r.[Id]
LEFT JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
LEFT JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
LEFT JOIN [TournamentGroups] tg ON m.[TournamentGroupId] = tg.[Id]
LEFT JOIN [TournamentStages] ts ON r.[TournamentStageId] = ts.[Id]
WHERE r.[Id] = @RoundId
ORDER BY tg.[GroupLetter], m.[MatchDateTimeUtc];
```

### Check Per-Match Deadline Status

```sql
SELECT
    m.[Id],
    m.[MatchDateTimeUtc],
    COALESCE(m.[CustomLockTimeUtc], r.[DeadlineUtc]) AS EffectiveDeadline,
    CASE
        WHEN COALESCE(m.[CustomLockTimeUtc], r.[DeadlineUtc]) < GETUTCDATE()
        THEN 1 ELSE 0
    END AS IsLocked
FROM [Matches] m
JOIN [Rounds] r ON m.[RoundId] = r.[Id]
WHERE r.[Id] = @RoundId;
```

---

## Appendix C: File Change Summary

### New Files

| File | Type | Description |
|------|------|-------------|
| `Domain/Common/Enumerations/CompetitionType.cs` | Enum | League, Tournament, Hybrid |
| `Domain/Common/Enumerations/TournamentStageType.cs` | Enum | Group, Knockout |
| `Domain/Models/TournamentStage.cs` | Entity | Stage lookup |
| `Domain/Models/TournamentGroup.cs` | Entity | Group A, B, etc. |
| `Application/Repositories/ITournamentStageRepository.cs` | Interface | Stage access |
| `Application/Repositories/ITournamentGroupRepository.cs` | Interface | Group access |
| `Infrastructure/Repositories/TournamentStageRepository.cs` | Implementation | Stage repo |
| `Infrastructure/Repositories/TournamentGroupRepository.cs` | Implementation | Group repo |
| `Application/Features/Admin/Rounds/Commands/CreateTournamentRoundCommand.cs` | Command | Create grouped round |
| `Contracts/Dashboard/MatchGroupDto.cs` | DTO | Grouped matches |
| `Web.Client/Components/Pages/Predictions/MatchLockBadge.razor` | Component | Lock indicator |

### Modified Files

| File | Changes |
|------|---------|
| `Domain/Models/Season.cs` | Add `CompetitionType` property |
| `Domain/Models/Round.cs` | Add tournament properties |
| `Domain/Models/Match.cs` | Add group/stage properties, deadline helpers |
| `Domain/Services/PredictionDomainService.cs` | Per-match deadline logic |
| `Contracts/Dashboard/MatchPredictionDto.cs` | Add lock status, placeholders |
| `Contracts/Predictions/PredictionPageDto.cs` | Add tournament properties |
| `Application/Features/Predictions/Queries/GetPredictionPageDataQueryHandler.cs` | Return deadline info |
| `Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs` | Tournament sync logic |
| `Web.Client/Components/Pages/Predictions/Predictions.razor` | Per-match UI |
| `Validators/Admin/Seasons/CreateSeasonRequestValidator.cs` | Validate competition type |

---

## Next Steps

1. Review this plan with stakeholders
2. Create task files for each phase (01-foundation.md, 02-per-match-deadlines.md, etc.)
3. Set up feature branch for development
4. Begin Phase 1 implementation

---

*Document Version: 1.0*
*Created: January 2026*
*Author: Claude (AI Assistant)*
