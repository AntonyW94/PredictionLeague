# Task 5: Rename Tile

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Rename the "Upcoming Rounds" tile to "Active Rounds" to better reflect its new purpose of showing both upcoming and in-progress rounds. This includes renaming the component file and updating all references.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `UpcomingRoundsTile.razor` | Rename | Rename to `ActiveRoundsTile.razor` |
| `ActiveRoundsTile.razor` | Modify | Update tile title and empty state message |
| `Dashboard.razor` | Modify | Update component reference |
| `DashboardStateService.cs` | Modify | Rename properties and methods |
| `IDashboardStateService.cs` | Modify | Rename interface members |

## Implementation Steps

### Step 1: Rename Component File

Rename the component file:

```
Components/Pages/Dashboard/UpcomingRoundsTile.razor
→ Components/Pages/Dashboard/ActiveRoundsTile.razor
```

### Step 2: Update Tile Title and Empty State

In `ActiveRoundsTile.razor`, update the title:

**Current:**
```razor
<SectionHeading>Upcoming Rounds</SectionHeading>
```

**New:**
```razor
<SectionHeading>Active Rounds</SectionHeading>
```

Update the empty state message:

**Current:**
```razor
<p>No upcoming rounds available.</p>
```

**New:**
```razor
<p>No active rounds available.</p>
```

### Step 3: Update Dashboard.razor Reference

Update the component reference in the parent dashboard page.

**Current:**
```razor
<UpcomingRoundsTile />
```

**New:**
```razor
<ActiveRoundsTile />
```

### Step 4: Update State Service Interface

In `IDashboardStateService.cs`, rename the members:

**Current:**
```csharp
List<UpcomingRoundDto> UpcomingRounds { get; }
bool IsUpcomingRoundsLoading { get; }
string? UpcomingRoundsErrorMessage { get; }
string? UpcomingRoundsSuccessMessage { get; }
Task LoadUpcomingRoundsAsync();
```

**New:**
```csharp
List<UpcomingRoundDto> ActiveRounds { get; }
bool IsActiveRoundsLoading { get; }
string? ActiveRoundsErrorMessage { get; }
string? ActiveRoundsSuccessMessage { get; }
Task LoadActiveRoundsAsync();
```

### Step 5: Update State Service Implementation

In `DashboardStateService.cs`, rename the corresponding members:

```csharp
public List<UpcomingRoundDto> ActiveRounds { get; private set; } = new();
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
        ActiveRounds = (await _leagueService.GetUpcomingRoundsAsync()).ToList();
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

### Step 6: Update Component to Use Renamed Service Members

In `ActiveRoundsTile.razor`, update references to use the new property/method names:

**Current:**
```razor
@if (DashboardState.IsUpcomingRoundsLoading)
{
    // loading spinner
}
else if (DashboardState.UpcomingRounds.Any())
{
    // carousel
}
```

**New:**
```razor
@if (DashboardState.IsActiveRoundsLoading)
{
    // loading spinner
}
else if (DashboardState.ActiveRounds.Any())
{
    // carousel
}
```

Also update `OnInitializedAsync`:

**Current:**
```csharp
await DashboardState.LoadUpcomingRoundsAsync();
```

**New:**
```csharp
await DashboardState.LoadActiveRoundsAsync();
```

## Code Patterns to Follow

Existing section heading pattern from other tiles:

```razor
<div class="section-tile">
    <SectionHeading>My Leagues</SectionHeading>
    ...
</div>
```

## Verification

- [ ] `UpcomingRoundsTile.razor` has been renamed to `ActiveRoundsTile.razor`
- [ ] Tile displays "Active Rounds" as its title
- [ ] Empty state message says "No active rounds available"
- [ ] Dashboard.razor uses `<ActiveRoundsTile />`
- [ ] State service interface has renamed members
- [ ] State service implementation has renamed members
- [ ] Component uses the new state service member names
- [ ] Solution builds without errors
- [ ] No remaining references to "UpcomingRounds" in the renamed areas

## Notes

- The DTOs (`UpcomingRoundDto`, `UpcomingMatchDto`) and query files are not renamed to limit the scope of changes
- The API endpoint and service method (`GetUpcomingRoundsAsync`) remain unchanged - only the state service layer is renamed
- This keeps the refactor focused on the UI layer while maintaining backwards compatibility
