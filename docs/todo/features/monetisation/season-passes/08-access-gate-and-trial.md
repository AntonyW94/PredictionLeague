# Task: Access Gate & Trial Logic

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase A — buildable now (no accounts).

## Status

**Not Started** | In Progress | Complete

## Goal

Enforce the Season Pass requirement when joining/creating a league in a pass-required season, and auto-grant a one-time free Standard trial to brand-new players.

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
  pass exists for (userId, seasonId)            -> allow (already participating this season)
  free season (PassStandardPrice IS NULL)              -> create £0 Free pass (burns the freebie) + allow
  else (paid season):
    user has ZERO SeasonPass records            -> grant free Trial pass (£0; first season free) + allow
    else                                        -> deny (purchase required)
```

- `SeasonAccessService.EnsureCanParticipateAsync(userId, seasonId)`:
  - If a pass exists for `(userId, seasonId)`, return.
  - If the season is **free** (`Season.RequiresPayment` is false, i.e. `PassStandardPrice IS NULL`), **create a £0 `Free` pass** (`SeasonPass.CreateFree`) and return — this records participation so a free season **burns the freebie**.
  - Else (paid): if the user has **zero `SeasonPass` records** (`COUNT(*) == 0`), **grant a free `Trial` pass** (`SeasonPass.CreateTrial`) — first season free — and return; otherwise throw `SeasonPassRequiredException(seasonId)`.
  - **Late entry needs no handling here:** the existing per-league entry-deadline rules already block joining once entry has closed (paid and free seasons alike) — ADR 0005.

### Step 2: Wire into Join

In `JoinLeagueCommandHandler.Handle`, after fetching the league and before `league.AddMember(...)`:

```csharp
await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league.SeasonId, cancellationToken);
```

### Step 3: Wire into Create

In `CreateLeagueCommandHandler`, gate before creating the league for the chosen season (creating a league in a pass-required season counts as taking part).

### Step 4: API surface

- Map `SeasonPassRequiredException` to a response the client can act on (e.g. 402/409 + seasonId) so the UI redirects to the purchase page (Task 10).
- Late/closed entry is reported by the **existing** join-deadline handling — no new exception needed.

## Code Patterns to Follow

- Commands use **repositories** (not `IApplicationReadDbConnection`).
- Use `IDateTimeProvider` for trial timestamps.
- Logging: `"Season pass trial granted for user (ID: {UserId}) in season (ID: {SeasonId})"`.

## Verification

- [ ] All branches covered (already-has-pass / free-season creates £0 Free pass / paid-season trial when 0 records / paid-season deny when ≥1 record).
- [ ] Worked examples in README all behave correctly.
- [ ] Free trial granted exactly once (only when 0 records on a paid season); never again once any record exists.
- [ ] Free-season (e.g. World Cup) play **creates a £0 `Free` record** and therefore **burns the freebie**.
- [ ] Domain coverage stays 100%.

## Edge Cases to Consider

- Concurrent first-join: unique index `(UserId, SeasonId)` prevents duplicate passes; handle the race gracefully (treat "already exists" as success).
- A user mid-season with a pass can join multiple leagues without re-charge.
- **Refunded pass still counts as a record** → a user who bought then refunded does **not** get a fresh free trial (prevents buy→refund→free gaming).
- Existing/free play is **backfilled** as £0 `Free` records (Task 07), so existing players already have records → they **pay** for their first paid season.

## Notes

Eligibility is a single cheap `COUNT(*)`/`EXISTS` over `SeasonPasses` for the user — no `LeagueMember` participation check needed. Confirm `League.SeasonId` is available on the loaded entity.
