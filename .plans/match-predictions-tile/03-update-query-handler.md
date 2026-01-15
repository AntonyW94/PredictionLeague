# Task 3: Update Query Handler

## Objective

Modify `GetUpcomingRoundsQueryHandler` to fetch matches with team logos and user predictions for each upcoming round.

## File to Modify

**Path:** `PredictionLeague.Application/Features/Dashboard/Queries/GetUpcomingRoundsQueryHandler.cs`

## Current State

The handler executes a single SQL query that returns:
- Round ID, Season Name, Round Number, Deadline
- A boolean indicating if the user has any predictions

It does NOT fetch individual matches or team data.

## Implementation Strategy

Use a **two-query approach** for cleaner, more maintainable code:

1. **Query 1:** Get upcoming rounds (existing query, slightly modified)
2. **Query 2:** Get all matches with predictions for those round IDs
3. **C# code:** Map matches to their respective rounds

This approach is cleaner than a single complex JOIN that would duplicate round data for each match.

## Target Implementation

Replace the entire file content with:

```csharp
using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetUpcomingRoundsQueryHandler : IRequestHandler<GetUpcomingRoundsQuery, IEnumerable<UpcomingRoundDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetUpcomingRoundsQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<UpcomingRoundDto>> Handle(GetUpcomingRoundsQuery request, CancellationToken cancellationToken)
    {
        // Query 1: Get upcoming rounds
        const string roundsSql = @"
            WITH ActiveMemberCount AS (
                SELECT
                    l.[SeasonId],
                    COUNT(DISTINCT lm.[UserId]) AS MemberCount
                FROM [LeagueMembers] lm
                JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
                WHERE lm.[Status] = @ApprovedStatus
                GROUP BY l.[SeasonId]
            )

            SELECT
                r.[Id],
                s.[Name] AS SeasonName,
                r.[RoundNumber],
                r.[DeadlineUtc],
                CAST(CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [UserPredictions] up
                        JOIN [Matches] m ON up.MatchId = m.Id
                        WHERE m.RoundId = r.Id AND up.UserId = @UserId
                    ) THEN 1
                    ELSE 0
                END AS bit) AS HasUserPredicted
            FROM
                [Rounds] r
            JOIN
                [Seasons] s ON r.[SeasonId] = s.[Id]
            LEFT JOIN
                [ActiveMemberCount] amc ON r.[SeasonId] = amc.[SeasonId]
            WHERE
                r.[Status] = @PublishedStatus
                AND r.[DeadlineUtc] > GETUTCDATE()
                AND r.[SeasonId] IN (
                    SELECT l.[SeasonId]
                    FROM [Leagues] l
                    JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
                    WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
                )
            ORDER BY
                r.[DeadlineUtc] ASC";

        var parameters = new
        {
            request.UserId,
            PublishedStatus = nameof(RoundStatus.Published),
            ApprovedStatus = nameof(LeagueMemberStatus.Approved)
        };

        var rounds = (await _dbConnection.QueryAsync<UpcomingRoundQueryResult>(
            roundsSql,
            cancellationToken,
            parameters)).ToList();

        if (!rounds.Any())
        {
            return Enumerable.Empty<UpcomingRoundDto>();
        }

        // Query 2: Get matches with predictions for all upcoming rounds
        var roundIds = rounds.Select(r => r.Id).ToArray();

        const string matchesSql = @"
            SELECT
                m.[RoundId],
                m.[Id] AS MatchId,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                m.[MatchDateTimeUtc],
                ht.[ShortName] AS HomeTeamShortName
            FROM [Matches] m
            INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
            INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
            LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
            WHERE m.[RoundId] IN @RoundIds
            ORDER BY m.[RoundId], m.[MatchDateTimeUtc] ASC, ht.[ShortName] ASC";

        var matches = await _dbConnection.QueryAsync<UpcomingMatchQueryResult>(
            matchesSql,
            cancellationToken,
            new { request.UserId, RoundIds = roundIds });

        // Group matches by RoundId for efficient lookup
        var matchesByRound = matches
            .GroupBy(m => m.RoundId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Map to DTOs
        return rounds.Select(r => new UpcomingRoundDto(
            r.Id,
            r.SeasonName,
            r.RoundNumber,
            r.DeadlineUtc,
            r.HasUserPredicted,
            matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m => new UpcomingMatchDto(
                    m.MatchId,
                    m.HomeTeamLogoUrl,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore,
                    m.PredictedAwayScore))
                : Enumerable.Empty<UpcomingMatchDto>()
        ));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UpcomingRoundQueryResult(
        int Id,
        string SeasonName,
        int RoundNumber,
        DateTime DeadlineUtc,
        bool HasUserPredicted);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UpcomingMatchQueryResult(
        int RoundId,
        int MatchId,
        string? HomeTeamLogoUrl,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        DateTime MatchDateTimeUtc,
        string HomeTeamShortName);
}
```

## Key Implementation Details

### Two-Query Approach

**Why not a single JOIN query?**
- A single query joining rounds with matches would duplicate round data for each match (10x duplication per round)
- Two queries with C# grouping is more efficient and readable
- Follows the pattern used elsewhere in the codebase (see `GetPredictionPageDataQueryHandler.cs`)

### Match Ordering

The SQL `ORDER BY` clause ensures matches are ordered by:
1. `RoundId` (for grouping)
2. `MatchDateTimeUtc ASC` (earliest kickoff first)
3. `HomeTeamShortName ASC` (alphabetical for same kickoff times)

### Private Record Types

- `UpcomingRoundQueryResult` - intermediate result from Query 1
- `UpcomingMatchQueryResult` - intermediate result from Query 2
- These include extra fields (`MatchDateTimeUtc`, `HomeTeamShortName`) needed for ordering but not exposed in the final DTO

### Null Handling

- `LEFT JOIN [UserPredictions]` returns NULL when user hasn't predicted
- Logo URLs can be NULL if team data is incomplete
- All nullable fields are properly typed as `string?` or `int?`

## Database Column Reference

### UserPredictions Table
```
PredictedHomeScore (int)
PredictedAwayScore (int)
MatchId (int, FK)
UserId (string, FK)
```

### Teams Table
```
Id (int, PK)
LogoUrl (string, nullable)
ShortName (string)
```

### Matches Table
```
Id (int, PK)
RoundId (int, FK)
HomeTeamId (int, FK)
AwayTeamId (int, FK)
MatchDateTimeUtc (datetime)
```

## Verification

After modifying the file:

1. Build the Application project:
   ```bash
   dotnet build PredictionLeague.Application/PredictionLeague.Application.csproj
   ```

2. Build the entire solution to ensure no breaking changes:
   ```bash
   dotnet build PredictionLeague.sln
   ```

3. Verify the SQL syntax is correct by running the API and calling the endpoint (manual testing)

## Performance Considerations

- Maximum 5 rounds × ~10 matches = ~50 match records
- Two small queries are fast
- Dictionary lookup O(1) for grouping
- No N+1 query problem

## Next Task

Proceed to [Task 4: Create Placeholder SVG](./04-create-placeholder-svg.md)
