# Task: Access Gate & Trial Logic

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase A — buildable now (no accounts).

## Status

Not Started | In Progress | **Complete**

> **Update (June 2026):** The email-confirmation precondition described below is temporarily suspended per ADR-0012 - unconfirmed users can currently acquire a season pass until verification emails ship.

> **Done (A4 + A4b).** Gate + acquisition + acquire UI + My/Available passes pages + per-season public-league visibility gating all shipped. Acquisition now also requires a confirmed email (Task 18). Paid (non-trial) acquisition still routes to Stripe checkout, which is Phase B (Task 09).

> **Acquire-first model (revised).** A Season Pass is required to take part in **every** season. The **gate** only checks the user already **holds** a pass for the season - it does **not** grant one. Acquisition is a separate, explicit action so users acquire before participating (and before seeing a season's public leagues).
> **Built (this PR):** the gate (`SeasonAccessService` = pass-exists check, else `SeasonPassRequiredException` → 402) and `AcquireSeasonPassCommand` (free season → £0 `Free`; brand-new user's first paid season → free `Trial`; paid non-trial → needs Stripe, Phase B), with the `SeasonPasses` table/repo/backfill and `SeasonPassRequiredException`→402 mapping (from the original A4 cut).
> **Follow-up tasks (not in this PR):** the acquire **API endpoint + UI** (the "Get your pass" page / 402 redirect), the **My Passes** + **Available Passes** pages, and **per-season public-league visibility gating**.
> **Deploy note:** do **not** deploy the gate change until the acquire UI exists, or free-season joining will 402 with no recovery path.

## Goal

Enforce that a user **holds** a Season Pass before joining/creating a league (the gate), and provide explicit **acquisition** of a free pass (free season = £0; brand-new user's first paid season = free trial). Paid acquisition is via Stripe (Phase B).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `...Application/Services/ISeasonAccessService.cs` + `SeasonAccessService.cs` | Create | The gate: does the user hold a pass for the season? |
| `...Application/Features/SeasonPasses/Commands/AcquireSeasonPassCommand(.Handler).cs` | Create | Explicit acquisition: free season → £0 `Free`; first paid season → `Trial`; paid non-trial → Stripe (Phase B) |
| `src/ThePredictions.Application/Repositories/ISeasonPassRepository.cs` | Create | Read/write passes (commands) |
| `src/ThePredictions.Infrastructure/Repositories/SeasonPassRepository.cs` | Create | Dapper implementation |
| `src/ThePredictions.Application/Features/Leagues/Commands/JoinLeagueCommandHandler.cs` | Modify | Gate before `AddMember` |
| `src/ThePredictions.Application/Features/Leagues/Commands/CreateLeagueCommandHandler.cs` | Modify | Gate before create |
| Domain exception | Create | `SeasonPassRequiredException` for API mapping (402) |

## Implementation Steps

### Step 1: The gate (holds-a-pass check)

```
EnsureCanParticipateAsync(userId, seasonId):
  pass exists for (userId, seasonId)  -> allow
  else                                -> throw SeasonPassRequiredException(seasonId)   (client routes to acquire)
```

- `SeasonAccessService.EnsureCanParticipateAsync(userId, seasonId)` does **only** the existence check above. It never creates a pass.

### Step 1b: Acquisition (explicit, separate)

`AcquireSeasonPassCommand(userId, seasonId)`:
  - pass already exists → return (idempotent).
  - **free season** (`!Season.RequiresPayment`, i.e. `PassStandardPrice IS NULL`) → create a £0 `Free` pass (`SeasonPass.CreateFree`) — records participation so a free season **burns the freebie**.
  - Else (**paid**): if the user has **zero `SeasonPass` records** (`COUNT(*) == 0`), **grant a free `Trial` pass** (`SeasonPass.CreateTrial`) — first season free; otherwise it must be paid for → **Stripe checkout (Phase B)**, not this free-acquire path.
  - **Late entry needs no handling here:** the existing per-league entry-deadline rules already block joining once entry has closed (paid and free seasons alike) — ADR 0005.

### Step 2: Wire into Join

In `JoinLeagueCommandHandler.Handle`, after fetching the league and before `league.AddMember(...)`:

```csharp
await seasonAccessService.EnsureCanParticipateAsync(request.JoiningUserId, league.SeasonId, cancellationToken);
```

### Step 3: Wire into Create

In `CreateLeagueCommandHandler`, gate before creating the league for the chosen season (creating a league in a pass-required season counts as taking part).

### Step 4: API surface

- `SeasonPassRequiredException` maps to **402** + `seasonId` in `ErrorHandlingMiddleware` so the client redirects to the acquire page.
- **Follow-up (next task):** an acquire endpoint (`POST` calling `AcquireSeasonPassCommand` with the current user) + the acquire UI; the **My/Available Passes** pages; and **per-season public-league visibility gating** (a season's public-league list is only shown once the user holds a pass for it). Joining a **private league by code** is already gated (same `JoinLeagueCommandHandler`).
- Late/closed entry is reported by the **existing** join-deadline handling — no new exception needed.

## Code Patterns to Follow

- Commands use **repositories** (not `IApplicationReadDbConnection`).
- Use `IDateTimeProvider` for trial timestamps.

## Verification

- [ ] Gate: holds-pass → allow; no pass → `SeasonPassRequiredException` (402); it never creates a pass.
- [ ] Acquire: already-has-pass → idempotent; free season → £0 `Free`; paid + 0 records → `Trial`; paid + ≥1 records → needs Stripe (Phase B).
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
