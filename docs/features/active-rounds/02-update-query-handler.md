# Task 2: Update Query Handler

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Modify the `GetUpcomingRoundsQueryHandler` to include in-progress rounds and return actual match scores, enabling the dashboard to display rounds with live prediction outcome feedback.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Dashboard/Queries/GetUpcomingRoundsQueryHandler.cs` | Modify | Update SQL and DTO mapping |

## Implementation Steps

### Step 1: Update the Rounds SQL Query

Modify the WHERE clause to include both:
- Published rounds with deadline in the future
- In-progress rounds (regardless of deadline)

Update the ORDER BY to show in-progress rounds first.

Add the Status column to the SELECT.

```sql
SELECT
    r.[Id],
    s.[Name] AS SeasonName,
    r.[RoundNumber],
    r.[DeadlineUtc],
    r.[Status],  -- NEW
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
    (
        (r.[Status] = @PublishedStatus AND r.[DeadlineUtc] > GETUTCDATE())
        OR r.[Status] = @InProgressStatus
    )
    AND r.[SeasonId] IN (
        SELECT l.[SeasonId]
        FROM [Leagues] l
        JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
        WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
    )
ORDER BY
    CASE WHEN r.[Status] = @InProgressStatus THEN 0 ELSE 1 END,
    r.[DeadlineUtc] ASC
```

### Step 2: Update the Matches SQL Query

Add actual score columns to the SELECT.

```sql
SELECT
    m.[RoundId],
    m.[Id] AS MatchId,
    ht.[LogoUrl] AS HomeTeamLogoUrl,
    at.[LogoUrl] AS AwayTeamLogoUrl,
    up.[PredictedHomeScore],
    up.[PredictedAwayScore],
    m.[ActualHomeTeamScore] AS ActualHomeScore,  -- NEW
    m.[ActualAwayTeamScore] AS ActualAwayScore,  -- NEW
    m.[MatchDateTimeUtc],
    ht.[ShortName] AS HomeTeamShortName
FROM [Matches] m
INNER JOIN [Teams] ht ON m.[HomeTeamId] = ht.[Id]
INNER JOIN [Teams] at ON m.[AwayTeamId] = at.[Id]
LEFT JOIN [UserPredictions] up ON up.[MatchId] = m.[Id] AND up.[UserId] = @UserId
WHERE m.[RoundId] IN @RoundIds
ORDER BY m.[RoundId], m.[MatchDateTimeUtc] ASC, ht.[ShortName] ASC
```

### Step 3: Update Query Parameters

Add the InProgress status parameter.

```csharp
var parameters = new
{
    request.UserId,
    PublishedStatus = nameof(RoundStatus.Published),
    InProgressStatus = nameof(RoundStatus.InProgress),  // NEW
    ApprovedStatus = nameof(LeagueMemberStatus.Approved)
};
```

### Step 4: Update Private Query Result Records

Update the internal record types to include new fields.

```csharp
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
private record UpcomingRoundQueryResult(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    string Status,  // NEW
    bool HasUserPredicted);

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
private record UpcomingMatchQueryResult(
    int RoundId,
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    int? ActualHomeScore,   // NEW
    int? ActualAwayScore,   // NEW
    DateTime MatchDateTimeUtc,
    string HomeTeamShortName);
```

### Step 5: Update DTO Mapping

Update the mapping to include the new fields.

```csharp
return rounds.Select(r => new UpcomingRoundDto(
    r.Id,
    r.SeasonName,
    r.RoundNumber,
    r.DeadlineUtc,
    r.HasUserPredicted,
    r.Status,  // NEW
    matchesByRound.TryGetValue(r.Id, out var roundMatches)
        ? roundMatches.Select(m => new UpcomingMatchDto(
            m.MatchId,
            m.HomeTeamLogoUrl,
            m.AwayTeamLogoUrl,
            m.PredictedHomeScore,
            m.PredictedAwayScore,
            m.ActualHomeScore,   // NEW
            m.ActualAwayScore))  // NEW
        : Enumerable.Empty<UpcomingMatchDto>()
));
```

## Full Updated Handler Code

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
        // Query 1: Get active rounds (upcoming + in-progress)
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
                r.[Status],
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
                (
                    (r.[Status] = @PublishedStatus AND r.[DeadlineUtc] > GETUTCDATE())
                    OR r.[Status] = @InProgressStatus
                )
                AND r.[SeasonId] IN (
                    SELECT l.[SeasonId]
                    FROM [Leagues] l
                    JOIN [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
                    WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
                )
            ORDER BY
                CASE WHEN r.[Status] = @InProgressStatus THEN 0 ELSE 1 END,
                r.[DeadlineUtc] ASC";

        var parameters = new
        {
            request.UserId,
            PublishedStatus = nameof(RoundStatus.Published),
            InProgressStatus = nameof(RoundStatus.InProgress),
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

        // Query 2: Get matches with predictions and actual scores for all active rounds
        var roundIds = rounds.Select(r => r.Id).ToArray();

        const string matchesSql = @"
            SELECT
                m.[RoundId],
                m.[Id] AS MatchId,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                m.[ActualHomeTeamScore] AS ActualHomeScore,
                m.[ActualAwayTeamScore] AS ActualAwayScore,
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
            r.Status,
            matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m => new UpcomingMatchDto(
                    m.MatchId,
                    m.HomeTeamLogoUrl,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore,
                    m.PredictedAwayScore,
                    m.ActualHomeScore,
                    m.ActualAwayScore))
                : Enumerable.Empty<UpcomingMatchDto>()
        ));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UpcomingRoundQueryResult(
        int Id,
        string SeasonName,
        int RoundNumber,
        DateTime DeadlineUtc,
        string Status,
        bool HasUserPredicted);

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UpcomingMatchQueryResult(
        int RoundId,
        int MatchId,
        string? HomeTeamLogoUrl,
        string? AwayTeamLogoUrl,
        int? PredictedHomeScore,
        int? PredictedAwayScore,
        int? ActualHomeScore,
        int? ActualAwayScore,
        DateTime MatchDateTimeUtc,
        string HomeTeamShortName);
}
```

## Code Patterns to Follow

From `GetMyLeaguesQueryHandler.cs` - similar pattern for handling multiple statuses:

```csharp
// Ordering by status priority
ORDER BY
    CASE
        WHEN r.[Status] = 'InProgress' THEN 1
        WHEN r.[Status] = 'Published' THEN 2
        ELSE 3
    END
```

## Verification

- [ ] Solution builds without errors
- [ ] Query returns both Published (future deadline) and InProgress rounds
- [ ] In-progress rounds appear before upcoming rounds in results
- [ ] Actual scores are returned for matches (null when not yet available)
- [ ] Round status is correctly returned as string ("Published" or "InProgress")

## Edge Cases to Consider

- Rounds with no matches - should still return the round with empty Matches collection
- Users not in any leagues - returns empty collection (existing behaviour)
- Mixed seasons - user might have rounds from multiple seasons; all should be included
- Actual scores partially set - if only home score is set (shouldn't happen, but defensive)

## Notes

- The `ActiveMemberCount` CTE isn't actually used in the current query but is left in place for potential future use
- Consider whether to remove it as dead code or keep for future requirements
- The ordering ensures in-progress rounds always appear first, which matches user expectations
