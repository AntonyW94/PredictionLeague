# Task 5: Rename to Active Rounds

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Rename all "UpcomingRounds" references to "ActiveRounds" across all layers for consistency. This includes DTOs, queries, services, and UI components.

## Files to Rename

| Current Name | New Name |
|--------------|----------|
| `UpcomingRoundDto.cs` | `ActiveRoundDto.cs` |
| `UpcomingMatchDto.cs` | `ActiveRoundMatchDto.cs` |
| `GetUpcomingRoundsQuery.cs` | `GetActiveRoundsQuery.cs` |
| `GetUpcomingRoundsQueryHandler.cs` | `GetActiveRoundsQueryHandler.cs` |
| `UpcomingRoundsTile.razor` | `ActiveRoundsTile.razor` |

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `ActiveRoundDto.cs` | Modify | Rename record to `ActiveRoundDto` |
| `ActiveRoundMatchDto.cs` | Modify | Rename record to `ActiveRoundMatchDto` |
| `GetActiveRoundsQuery.cs` | Modify | Rename class to `GetActiveRoundsQuery` |
| `GetActiveRoundsQueryHandler.cs` | Modify | Rename class, update references |
| `ILeagueService.cs` | Modify | Rename method |
| `LeagueService.cs` | Modify | Rename method |
| `LeaguesController.cs` | Modify | Update endpoint and method |
| `IDashboardStateService.cs` | Modify | Rename properties and methods |
| `DashboardStateService.cs` | Modify | Rename properties and methods |
| `ActiveRoundsTile.razor` | Modify | Update title, references |
| `RoundCard.razor` | Modify | Update DTO type reference |
| `Dashboard.razor` | Modify | Update component reference |

## Implementation Steps

### Step 1: Rename and Update DTOs

**Rename files:**
```
PredictionLeague.Contracts/Dashboard/UpcomingRoundDto.cs
→ PredictionLeague.Contracts/Dashboard/ActiveRoundDto.cs

PredictionLeague.Contracts/Dashboard/UpcomingMatchDto.cs
→ PredictionLeague.Contracts/Dashboard/ActiveRoundMatchDto.cs
```

**Update `ActiveRoundDto.cs`:**
```csharp
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// DTO for displaying active rounds (upcoming + in-progress) on the dashboard tile.
/// </summary>
public record ActiveRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    RoundStatus Status,
    IEnumerable<ActiveRoundMatchDto> Matches
);
```

**Update `ActiveRoundMatchDto.cs`:**
```csharp
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// Lightweight DTO for displaying match predictions on the dashboard active rounds tile.
/// </summary>
public record ActiveRoundMatchDto(
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    PredictionOutcome? Outcome
);
```

### Step 2: Rename and Update Query Files

**Rename files:**
```
PredictionLeague.Application/Features/Dashboard/Queries/GetUpcomingRoundsQuery.cs
→ PredictionLeague.Application/Features/Dashboard/Queries/GetActiveRoundsQuery.cs

PredictionLeague.Application/Features/Dashboard/Queries/GetUpcomingRoundsQueryHandler.cs
→ PredictionLeague.Application/Features/Dashboard/Queries/GetActiveRoundsQueryHandler.cs
```

**Update `GetActiveRoundsQuery.cs`:**
```csharp
using MediatR;
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetActiveRoundsQuery(string UserId) : IRequest<IEnumerable<ActiveRoundDto>>;
```

**Update `GetActiveRoundsQueryHandler.cs`:**
```csharp
public class GetActiveRoundsQueryHandler : IRequestHandler<GetActiveRoundsQuery, IEnumerable<ActiveRoundDto>>
{
    // ... update all references to use ActiveRoundDto and ActiveRoundMatchDto
}
```

### Step 3: Update API Service Interface and Implementation

**Update `ILeagueService.cs`:**
```csharp
// Change:
Task<IEnumerable<UpcomingRoundDto>> GetUpcomingRoundsAsync();

// To:
Task<IEnumerable<ActiveRoundDto>> GetActiveRoundsAsync();
```

**Update `LeagueService.cs`:**
```csharp
public async Task<IEnumerable<ActiveRoundDto>> GetActiveRoundsAsync()
{
    // Update implementation
}
```

### Step 4: Update API Controller

**Update `LeaguesController.cs`:**
```csharp
// Change:
[HttpGet("upcoming-rounds")]
public async Task<ActionResult<IEnumerable<UpcomingRoundDto>>> GetUpcomingRounds()
{
    var query = new GetUpcomingRoundsQuery(UserId);
    // ...
}

// To:
[HttpGet("active-rounds")]
public async Task<ActionResult<IEnumerable<ActiveRoundDto>>> GetActiveRounds()
{
    var query = new GetActiveRoundsQuery(UserId);
    // ...
}
```

**Note:** Changing the endpoint from `upcoming-rounds` to `active-rounds` is a breaking change. If there are external consumers, consider keeping the old endpoint as an alias or deprecated route.

### Step 5: Update Dashboard State Service

**Update `IDashboardStateService.cs`:**
```csharp
List<ActiveRoundDto> ActiveRounds { get; }
bool IsActiveRoundsLoading { get; }
string? ActiveRoundsErrorMessage { get; }
string? ActiveRoundsSuccessMessage { get; }
Task LoadActiveRoundsAsync();
```

**Update `DashboardStateService.cs`:**
```csharp
public List<ActiveRoundDto> ActiveRounds { get; private set; } = new();
public bool IsActiveRoundsLoading { get; private set; }
public string? ActiveRoundsErrorMessage { get; private set; }
public string? ActiveRoundsSuccessMessage { get; private set; }

public async Task LoadActiveRoundsAsync()
{
    IsActiveRoundsLoading = true;
    ActiveRoundsErrorMessage = null;
    NotifyStateChanged();

    try
    {
        ActiveRounds = (await _leagueService.GetActiveRoundsAsync()).ToList();
    }
    catch (Exception ex)
    {
        ActiveRoundsErrorMessage = "Failed to load active rounds.";
        _logger.LogError(ex, "Error loading active rounds");
    }
    finally
    {
        IsActiveRoundsLoading = false;
        NotifyStateChanged();
    }
}
```

### Step 6: Update Blazor Components

**Rename file:**
```
Components/Pages/Dashboard/UpcomingRoundsTile.razor
→ Components/Pages/Dashboard/ActiveRoundsTile.razor
```

**Update `ActiveRoundsTile.razor`:**
- Change title to "Active Rounds"
- Update empty state message to "No active rounds available"
- Update all `DashboardState.UpcomingRounds` → `DashboardState.ActiveRounds`
- Update all `IsUpcomingRoundsLoading` → `IsActiveRoundsLoading`
- Update `LoadUpcomingRoundsAsync()` → `LoadActiveRoundsAsync()`

**Update `RoundCard.razor`:**
```csharp
// Change parameter type:
[Parameter, EditorRequired]
public ActiveRoundDto Round { get; set; } = null!;

// Update method signature:
private string GetMatchBackgroundClass(ActiveRoundMatchDto match)
```

**Update `Dashboard.razor`:**
```razor
// Change:
<UpcomingRoundsTile />

// To:
<ActiveRoundsTile />
```

## Verification

- [ ] All files renamed correctly
- [ ] `ActiveRoundDto` and `ActiveRoundMatchDto` records updated
- [ ] `GetActiveRoundsQuery` and handler updated
- [ ] API service interface and implementation updated
- [ ] API controller endpoint updated to `active-rounds`
- [ ] Dashboard state service interface and implementation updated
- [ ] `ActiveRoundsTile.razor` component updated
- [ ] `RoundCard.razor` parameter types updated
- [ ] `Dashboard.razor` component reference updated
- [ ] Solution builds without errors
- [ ] No remaining references to "Upcoming" in renamed areas

## Order of Changes

To minimise build errors during refactoring, follow this order:

1. Rename and update DTOs first (Contracts layer)
2. Rename and update Query/Handler (Application layer)
3. Update API service and controller (API layer)
4. Update state service (Client layer)
5. Rename and update Blazor components (Client layer)

## Notes

- The API endpoint changes from `/api/leagues/upcoming-rounds` to `/api/leagues/active-rounds`
- All layers are renamed for full consistency
- Use IDE refactoring tools (Rename Symbol) where possible to catch all references
- Run a full solution build after each layer to catch any missed references
