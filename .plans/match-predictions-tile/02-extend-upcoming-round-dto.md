# Task 2: Extend UpcomingRoundDto

## Objective

Add a `Matches` property to the existing `UpcomingRoundDto` to include match data with predictions.

## File to Modify

**Path:** `PredictionLeague.Contracts/Dashboard/UpcomingRoundDto.cs`

## Current State

```csharp
namespace PredictionLeague.Contracts.Dashboard;

public record UpcomingRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted);
```

## Target State

```csharp
namespace PredictionLeague.Contracts.Dashboard;

public record UpcomingRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    IEnumerable<UpcomingMatchDto> Matches);
```

## Implementation Steps

1. Open `PredictionLeague.Contracts/Dashboard/UpcomingRoundDto.cs`

2. Add `IEnumerable<UpcomingMatchDto> Matches` as the last parameter in the record definition

3. No `using` statement needed as `UpcomingMatchDto` is in the same namespace

## Why IEnumerable instead of List or Array?

- `IEnumerable<T>` is the most flexible return type
- Allows the query handler to use any collection type internally
- Consistent with other DTOs in the codebase that return collections
- Read-only semantics (cannot add/remove items)

## Verification

After modifying the file, verify:

1. The file compiles without errors:
   ```bash
   dotnet build PredictionLeague.Contracts/PredictionLeague.Contracts.csproj
   ```

2. The `UpcomingMatchDto` type is recognized (Task 1 must be completed first)

## Breaking Change Note

This change will cause a compile error in `GetUpcomingRoundsQueryHandler.cs` because the constructor now requires a `Matches` parameter. This is expected and will be fixed in Task 3.

## Next Task

Proceed to [Task 3: Update Query Handler](./03-update-query-handler.md)
