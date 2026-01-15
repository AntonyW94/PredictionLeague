# Task 1: Create UpcomingMatchDto

## Objective

Create a new lightweight DTO to represent a match with team logos and user predictions for the dashboard tile.

## File to Create

**Path:** `PredictionLeague.Contracts/Dashboard/UpcomingMatchDto.cs`

## Implementation

Create the following file:

```csharp
namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// Lightweight DTO for displaying match predictions on the dashboard upcoming rounds tile.
/// Contains only the data needed for the compact match preview (logos and scores).
/// </summary>
public record UpcomingMatchDto(
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore
);
```

## Design Decisions

### Why a new DTO instead of reusing MatchPredictionDto?

The existing `MatchPredictionDto` (in `PredictionLeague.Contracts/Dashboard/MatchPredictionDto.cs`) contains many fields not needed for this compact view:
- `MatchDateTimeUtc`
- `HomeTeamName`
- `HomeTeamShortName`
- `HomeTeamAbbreviation`
- `AwayTeamName`
- `AwayTeamShortName`
- `AwayTeamAbbreviation`

Creating a lightweight DTO reduces:
1. Data transferred over the wire
2. Memory usage on the client
3. Complexity in the component

### Why nullable logo URLs?

Team logos come from an external API (api-sports.io). While rare, logos could be:
- Not yet populated for a new team
- Removed or unavailable from the API

The frontend handles null URLs with a placeholder SVG.

### Why nullable predicted scores?

Users may not have submitted predictions yet. Null indicates "no prediction" which displays as "-" in the UI.

## Verification

After creating the file, verify:

1. The file compiles without errors:
   ```bash
   dotnet build PredictionLeague.Contracts/PredictionLeague.Contracts.csproj
   ```

2. The namespace matches the folder structure (`PredictionLeague.Contracts.Dashboard`)

3. The record uses C# 10+ positional syntax (consistent with other DTOs in the project)

## Next Task

Proceed to [Task 2: Extend UpcomingRoundDto](./02-extend-upcoming-round-dto.md)
