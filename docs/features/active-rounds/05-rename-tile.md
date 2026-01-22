# Task 5: Rename Tile

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Rename the "Upcoming Rounds" tile to "Active Rounds" to better reflect its new purpose of showing both upcoming and in-progress rounds.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web/PredictionLeague.Web.Client/Components/Pages/Dashboard/UpcomingRoundsTile.razor` | Modify | Update tile title |

## Implementation Steps

### Step 1: Update the Tile Title

Locate the section title and change "Upcoming Rounds" to "Active Rounds".

**Current code (look for the title element):**
```razor
<SectionHeading>Upcoming Rounds</SectionHeading>
```
or
```razor
<h4>Upcoming Rounds</h4>
```

**New code:**
```razor
<SectionHeading>Active Rounds</SectionHeading>
```
or
```razor
<h4>Active Rounds</h4>
```

### Step 2: Update Empty State Message (if applicable)

If there's an empty state message mentioning "upcoming rounds", update it to say "active rounds".

**Look for text like:**
```razor
<p>No upcoming rounds available.</p>
```

**Change to:**
```razor
<p>No active rounds available.</p>
```

### Step 3: Consider File Rename (Optional)

While not strictly necessary, consider whether to rename the files for consistency:

| Current Name | Potential New Name |
|--------------|-------------------|
| `UpcomingRoundsTile.razor` | `ActiveRoundsTile.razor` |
| `UpcomingRoundDto.cs` | `ActiveRoundDto.cs` |
| `UpcomingMatchDto.cs` | `ActiveMatchDto.cs` |
| `GetUpcomingRoundsQuery.cs` | `GetActiveRoundsQuery.cs` |
| `GetUpcomingRoundsQueryHandler.cs` | `GetActiveRoundsQueryHandler.cs` |

**Recommendation:** Keep existing file names to avoid breaking changes and extensive refactoring. The tile title change is sufficient for user-facing clarity. File names can be updated in a future cleanup task if desired.

## Code Patterns to Follow

Existing section heading pattern from other tiles:

```razor
<div class="section-tile">
    <SectionHeading>My Leagues</SectionHeading>
    ...
</div>
```

## Verification

- [ ] Tile displays "Active Rounds" as its title
- [ ] Empty state message says "No active rounds" (if applicable)
- [ ] No other references to "Upcoming Rounds" remain in user-visible text
- [ ] Component builds without errors

## Edge Cases to Consider

- Screen readers / accessibility - the new title should be equally clear
- Mobile display - "Active Rounds" fits within the same space as "Upcoming Rounds"

## Notes

- This is a simple text change but important for user understanding
- "Active Rounds" better describes the tile's purpose: rounds you're currently participating in
- The file rename is optional and can be deferred to avoid a large refactor
- If file rename is done later, remember to update:
  - Component references in parent components
  - State service method names (e.g., `LoadUpcomingRoundsAsync` → `LoadActiveRoundsAsync`)
  - CSS classes if any are named after the component
