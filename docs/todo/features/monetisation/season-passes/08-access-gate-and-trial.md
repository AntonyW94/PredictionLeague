# Task: Access Gate & Trial Logic

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Enforce the Season Pass requirement when joining/creating a league in a pass-required season, and auto-grant a one-time free Entry trial to brand-new players.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Features/...Passes/Services/ISeasonAccessService.cs` | Create | Access decision contract |
| `...Passes/Services/SeasonAccessService.cs` | Create | Implements the access/trial rule |
| `src/ThePredictions.Application/Repositories/ISeasonPassRepository.cs` | Create | Read/write passes (commands) |
| `src/ThePredictions.Infrastructure/Repositories/SeasonPassRepository.cs` | Create | Dapper implementation |
| `src/ThePredictions.Application/Features/Leagues/Commands/JoinLeagueCommandHandler.cs` | Modify | Gate before `AddMember` |
| `src/ThePredictions.Application/Features/Leagues/Commands/CreateLeagueCommandHandler.cs` | Modify | Gate before create |
| Contracts/Domain exception | Create | e.g. `SeasonPassRequiredException` for API mapping |

## Implementation Steps

### Step 1: Access decision (authoritative rule — see README)

```
allowed if:
  1. season.RequiresPass == false                          -> allow (free season)
  2. pass exists for (userId, seasonId)                    -> allow
  3. user has NEVER participated before
     (no approved LeagueMember in ANY season
      AND no SeasonPass of any kind)                        -> grant Trial (Entry) + allow
  else                                                      -> deny (purchase required)
```

- `SeasonAccessService.EnsureCanParticipateAsync(userId, seasonId)`:
  - Loads the season; if not `RequiresPass`, return.
  - Checks for existing pass; if present, return.
  - Checks participation history (`ISeasonPassRepository` + a membership check); if none, **create a Trial pass** (`SeasonPass.CreateTrial`) via repository and return.
  - Otherwise throw `SeasonPassRequiredException(seasonId)`.

### Step 2: Wire into Join

In `JoinLeagueCommandHandler.Handle`, after fetching the league and before `league.AddMember(...)`:

```csharp
await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league.SeasonId, cancellationToken);
```

### Step 3: Wire into Create

In `CreateLeagueCommandHandler`, gate before creating the league for the chosen season (creating a league in a pass-required season counts as taking part).

### Step 4: API surface

- Map `SeasonPassRequiredException` to a response the client can act on (e.g. 402/409 + seasonId) so the UI redirects to the purchase page (Task 10).

## Code Patterns to Follow

- Commands use **repositories** (not `IApplicationReadDbConnection`).
- Use `IDateTimeProvider` for trial timestamps.
- Logging: `"Season pass trial granted for user (ID: {UserId}) in season (ID: {SeasonId})"`.

## Verification

- [ ] All four rule branches covered by tests (free season / has pass / trial grant / deny).
- [ ] Worked examples in README all behave correctly.
- [ ] Trial granted exactly once per user lifetime.
- [ ] Domain coverage stays 100%.

## Edge Cases to Consider

- Concurrent first-join: unique index `(UserId, SeasonId)` prevents duplicate passes; handle the race gracefully.
- A user mid-season with a pass can join multiple leagues without re-charge.
- World Cup participation must count as "participated" so it burns trial eligibility.

## Notes

Keep the participation-history check efficient (single existence query). Confirm `League.SeasonId` is available on the loaded entity.
