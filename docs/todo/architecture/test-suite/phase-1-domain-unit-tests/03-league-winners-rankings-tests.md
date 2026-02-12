# Task: League Winners and Rankings Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the four winner/ranking calculation methods on the `League` entity: `GetRoundWinners`, `GetPeriodWinners`, `GetOverallRankings`, and `GetMostExactScoresWinners`. These contain the core scoring and ranking business logic.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/LeagueWinnersAndRankingsTests.cs` | Create | Winner and ranking calculation tests |

## Implementation Steps

### Step 1: Create test helper for building leagues with members and results

All four methods depend on `League.Members` containing `LeagueMember` objects with `RoundResults`. Use the public constructor to build test data:

```csharp
private static League CreateLeagueWithMembers(params (string UserId, (int RoundId, int BoostedPoints, int ExactScoreCount)[] Results)[] members)
{
    var leagueMembers = members.Select(m =>
        new LeagueMember(
            leagueId: 1,
            userId: m.UserId,
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false,
            joinedAtUtc: DateTime.UtcNow,
            approvedAtUtc: DateTime.UtcNow,
            roundResults: m.Results.Select(r => new LeagueRoundResult
            {
                LeagueId = 1,
                RoundId = r.RoundId,
                UserId = m.UserId,
                BoostedPoints = r.BoostedPoints,
                ExactScoreCount = r.ExactScoreCount
            }).ToList()
        )).ToList();

    return new League(
        id: 1, name: "Test League", seasonId: 1,
        administratorUserId: "admin", entryCode: "ABC123",
        createdAtUtc: DateTime.UtcNow,
        entryDeadlineUtc: DateTime.UtcNow.AddDays(30),
        pointsForExactScore: 3, pointsForCorrectResult: 1,
        price: 0, isFree: true, hasPrizes: false,
        prizeFundOverride: null,
        members: leagueMembers, prizeSettings: null);
}
```

### Step 2: GetRoundWinners tests

| Test | Scenario | Expected |
|------|----------|----------|
| `GetRoundWinners_ShouldReturnEmptyList_WhenNoMembers` | Empty league | `[]` |
| `GetRoundWinners_ShouldReturnEmptyList_WhenAllScoresAreZero` | All members have 0 BoostedPoints | `[]` |
| `GetRoundWinners_ShouldReturnSingleWinner_WhenOnePlayerHasHighestScore` | User-A: 10pts, User-B: 5pts | `[User-A]` |
| `GetRoundWinners_ShouldReturnMultipleWinners_WhenPlayersAreTied` | User-A: 10pts, User-B: 10pts | `[User-A, User-B]` |
| `GetRoundWinners_ShouldReturnAllTiedMembers_WhenThreePlayersAreTied` | A: 10, B: 10, C: 10 | `[User-A, User-B, User-C]` |
| `GetRoundWinners_ShouldReturnEmptyList_WhenNoResultsForRound` | Members have results for other rounds only | `[]` |
| `GetRoundWinners_ShouldReturnEmptyList_WhenMembersHaveNoRoundResults` | Members exist but empty RoundResults | `[]` |
| `GetRoundWinners_ShouldUseBoostPoints_NotBasePoints` | BasePoints differs from BoostedPoints | Winner determined by BoostedPoints |
| `GetRoundWinners_ShouldReturnSingleMember_WhenOnlyOneMemberWithPoints` | One member, 5pts | `[User-A]` |
| `GetRoundWinners_ShouldReturnEmptyList_WhenOnlyOneMemberWithZeroPoints` | One member, 0pts | `[]` |

### Step 3: GetPeriodWinners tests

| Test | Scenario | Expected |
|------|----------|----------|
| `GetPeriodWinners_ShouldReturnEmptyList_WhenNoMembers` | Empty league | `[]` |
| `GetPeriodWinners_ShouldSumPointsAcrossRounds_WhenMultipleRoundsInPeriod` | User-A: 5+5=10, User-B: 3+8=11 | `[User-B]` |
| `GetPeriodWinners_ShouldReturnMultipleWinners_WhenTied` | Both sum to 10 | `[User-A, User-B]` |
| `GetPeriodWinners_ShouldIgnoreRoundsOutsidePeriod` | Only sum rounds in the given IDs | Correct filtering |
| `GetPeriodWinners_ShouldReturnEmptyList_WhenAllTotalScoresAreZero` | All zero | `[]` |
| `GetPeriodWinners_ShouldReturnEmptyList_WhenMembersHaveNoRoundResults` | Members exist but empty RoundResults | `[]` |
| `GetPeriodWinners_ShouldReturnEmptyList_WhenEmptyRoundIdsList` | Empty `roundIdsInPeriod` | `[]` |
| `GetPeriodWinners_ShouldHandleMembersWithPartialResults` | A has results for rounds 1+2, B only round 1 | Sum only matching rounds |

### Step 4: GetOverallRankings tests

Note: Unlike `GetRoundWinners`/`GetPeriodWinners`, `GetOverallRankings` does NOT filter out zero scores — it ranks all members.

| Test | Scenario | Expected |
|------|----------|----------|
| `GetOverallRankings_ShouldReturnEmptyList_WhenNoMembers` | Empty league | `[]` |
| `GetOverallRankings_ShouldRankByTotalBoostedPoints` | A:20, B:15, C:10 | Rank 1=A, Rank 2=B, Rank 3=C |
| `GetOverallRankings_ShouldGroupTiedPlayers_WhenScoresAreEqual` | A:20, B:20, C:10 | Rank 1=[A,B], Rank 3=C |
| `GetOverallRankings_ShouldSkipRanks_WhenPlayersAreTied` | A:20, B:20, C:10 | Rank 1, then Rank 3 (not 2) |
| `GetOverallRankings_ShouldHandleAllPlayersTied` | All have 10 points | Rank 1 with all members |
| `GetOverallRankings_ShouldSumAcrossAllRounds` | Multiple rounds per member | Total is sum of all BoostedPoints |
| `GetOverallRankings_ShouldIncludeAllZeroScoreMembers` | All members have 0 points | Rank 1 with all (unlike GetRoundWinners which returns empty) |
| `GetOverallRankings_ShouldReturnSingleRanking_WhenOnlyOneMember` | One member, 10pts | Rank 1=[User-A] |
| `GetOverallRankings_ShouldHandleMembersWithNoRoundResults` | Members exist but empty RoundResults | Rank 1 with all (0 points each) |
| `GetOverallRankings_ShouldHandleMultipleTiedGroups` | A:30, B:30, C:20, D:20, E:10 | Rank 1=[A,B], Rank 3=[C,D], Rank 5=[E] |

### Step 5: GetMostExactScoresWinners tests

| Test | Scenario | Expected |
|------|----------|----------|
| `GetMostExactScoresWinners_ShouldReturnEmptyList_WhenNoMembers` | Empty league | `[]` |
| `GetMostExactScoresWinners_ShouldReturnEmptyList_WhenNoExactScores` | All ExactScoreCount = 0 | `[]` |
| `GetMostExactScoresWinners_ShouldReturnSingleWinner_WhenOneHasMost` | A:5, B:3 | `[User-A]` |
| `GetMostExactScoresWinners_ShouldReturnMultipleWinners_WhenTied` | A:5, B:5 | `[User-A, User-B]` |
| `GetMostExactScoresWinners_ShouldSumExactScoresAcrossRounds` | A: 2+3=5, B: 4+0=4 | `[User-A]` |
| `GetMostExactScoresWinners_ShouldReturnEmptyList_WhenMembersHaveNoRoundResults` | Members exist but empty RoundResults | `[]` |
| `GetMostExactScoresWinners_ShouldReturnSingleMember_WhenOnlyOneMemberHasExactScores` | A:3, B:0 | `[User-A]` |

## Verification

- [ ] Empty league returns empty lists (not exceptions)
- [ ] All-zero scores return empty lists for GetRoundWinners/GetPeriodWinners/GetMostExactScoresWinners
- [ ] All-zero scores return all members in GetOverallRankings (different behaviour — ranks all members)
- [ ] Ties return multiple winners/grouped rankings
- [ ] Rank skipping works correctly (1, 1, 3 — not 1, 1, 2)
- [ ] Multiple tied groups handled (1, 1, 3, 3, 5)
- [ ] Only BoostedPoints are used (not BasePoints)
- [ ] Period filtering only sums specified rounds
- [ ] Empty round IDs list handled
- [ ] Members with no round results handled
- [ ] Single member scenario handled
- [ ] `dotnet test` passes

## Edge Cases to Consider

- League with members but no round results (empty `RoundResults` collection)
- Single member in league (always the winner unless zero score, except GetOverallRankings always includes them)
- Members with results for some rounds but not others (missing rounds treated as 0)
- Difference between `GetOverallRankings` (includes zero-score members) vs other methods (excludes them)
- Empty `roundIdsInPeriod` for `GetPeriodWinners`
