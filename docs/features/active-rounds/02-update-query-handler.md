# Task 2: Update Query Handler

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Modify the `GetUpcomingRoundsQueryHandler` to include in-progress rounds and return the pre-calculated prediction outcome from the `UserPredictions` table.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Application/Features/Dashboard/Queries/GetUpcomingRoundsQueryHandler.cs` | Modify | Update SQL and DTO mapping |

## Implementation Steps

### Step 1: Update the Rounds SQL Query

Modify the WHERE clause to include all rounds that are not Draft and not Completed:
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
WHERE
    r.[Status] NOT IN (@DraftStatus, @CompletedStatus)
    AND (r.[Status] = @InProgressStatus OR r.[DeadlineUtc] > GETUTCDATE())
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

**Note:** The WHERE clause uses:
- `NOT IN (Draft, Completed)` to exclude those statuses
- Additional check for `InProgress OR DeadlineUtc > GETUTCDATE()` to exclude Published rounds with past deadlines

### Step 2: Update the Matches SQL Query

Select the `Outcome` from `UserPredictions` instead of actual scores.

```sql
SELECT
    m.[RoundId],
    m.[Id] AS MatchId,
    ht.[LogoUrl] AS HomeTeamLogoUrl,
    at.[LogoUrl] AS AwayTeamLogoUrl,
    up.[PredictedHomeScore],
    up.[PredictedAwayScore],
    up.[Outcome],
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

Add the status parameters.

```csharp
var parameters = new
{
    request.UserId,
    DraftStatus = nameof(RoundStatus.Draft),
    CompletedStatus = nameof(RoundStatus.Completed),
    InProgressStatus = nameof(RoundStatus.InProgress),
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
    string? Outcome,
    DateTime MatchDateTimeUtc,
    string HomeTeamShortName);
```

**Note:** `Status` and `Outcome` are strings from SQL. They will be parsed to enums during mapping.

### Step 5: Update DTO Mapping

Parse the status and outcome strings to enums when mapping to DTOs.

```csharp
return rounds.Select(r => new UpcomingRoundDto(
    r.Id,
    r.SeasonName,
    r.RoundNumber,
    r.DeadlineUtc,
    r.HasUserPredicted,
    Enum.Parse<RoundStatus>(r.Status),
    matchesByRound.TryGetValue(r.Id, out var roundMatches)
        ? roundMatches.Select(m => new UpcomingMatchDto(
            m.MatchId,
            m.HomeTeamLogoUrl,
            m.AwayTeamLogoUrl,
            m.PredictedHomeScore,
            m.PredictedAwayScore,
            m.Outcome != null ? Enum.Parse<PredictionOutcome>(m.Outcome) : null))
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
            WHERE
                r.[Status] NOT IN (@DraftStatus, @CompletedStatus)
                AND (r.[Status] = @InProgressStatus OR r.[DeadlineUtc] > GETUTCDATE())
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
            DraftStatus = nameof(RoundStatus.Draft),
            CompletedStatus = nameof(RoundStatus.Completed),
            InProgressStatus = nameof(RoundStatus.InProgress),
            ApprovedStatus = nameof(LeagueMemberStatus.Approved)
        };

        var rounds = (await _dbConnection.QueryAsync<UpcomingRoundQueryResult>(
            roundsSql,
            cancellationToken,
            parameters)).ToList();

        if (!rounds.Any())
            return Enumerable.Empty<UpcomingRoundDto>();

        // Query 2: Get matches with predictions and outcomes for all active rounds
        var roundIds = rounds.Select(r => r.Id).ToArray();

        const string matchesSql = @"
            SELECT
                m.[RoundId],
                m.[Id] AS MatchId,
                ht.[LogoUrl] AS HomeTeamLogoUrl,
                at.[LogoUrl] AS AwayTeamLogoUrl,
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome],
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
            Enum.Parse<RoundStatus>(r.Status),
            matchesByRound.TryGetValue(r.Id, out var roundMatches)
                ? roundMatches.Select(m => new UpcomingMatchDto(
                    m.MatchId,
                    m.HomeTeamLogoUrl,
                    m.AwayTeamLogoUrl,
                    m.PredictedHomeScore,
                    m.PredictedAwayScore,
                    m.Outcome != null ? Enum.Parse<PredictionOutcome>(m.Outcome) : null))
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
        string? Outcome,
        DateTime MatchDateTimeUtc,
        string HomeTeamShortName);
}
```

## Code Patterns to Follow

From existing query handlers - parsing enum from string:

```csharp
Enum.Parse<RoundStatus>(r.Status)
```

For nullable enum parsing:

```csharp
m.Outcome != null ? Enum.Parse<PredictionOutcome>(m.Outcome) : null
```

## Verification

- [ ] Solution builds without errors
- [ ] Query returns both Published (future deadline) and InProgress rounds
- [ ] Draft and Completed rounds are excluded
- [ ] In-progress rounds appear before upcoming rounds in results
- [ ] Outcome is returned from UserPredictions (null when no prediction exists)
- [ ] Round status is correctly parsed to `RoundStatus` enum

## Edge Cases to Consider

- Rounds with no matches - should still return the round with empty Matches collection
- Users not in any leagues - returns empty collection (existing behaviour)
- Mixed seasons - user might have rounds from multiple seasons; all should be included
- No prediction for a match - `Outcome` will be null (LEFT JOIN returns null)
- Published round with past deadline but not yet InProgress - excluded (edge case during status transition)

## Notes

- Using `NOT IN (Draft, Completed)` is cleaner than explicitly listing included statuses
- The `Outcome` comes directly from `UserPredictions.[Outcome]` column - no calculation needed
- Removed the unused `ActiveMemberCount` CTE from the original query
- The internal query result records use strings for enums because Dapper returns them as strings
