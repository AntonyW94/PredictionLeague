# Task: Domain Model — Season Pass

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the `RequiresPass` flag and admin-set prices to `Season`, and introduce the `SeasonPass` domain entity (with SMS usage + reward tracking) and supporting enums.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Season.cs` | Modify | Add `RequiresPass`, prices, and `CompetitionId` (FK); **drop `ApiLeagueId` and `CompetitionType`** (both move to `Competitions`, ADR 0017) |
| `src/ThePredictions.Domain/Models/SeasonPass.cs` | Create | New entity |
| `src/ThePredictions.Domain/Common/Enumerations/SeasonPassTier.cs` | Create | `Entry`, `EntryPlusSms` |
| `src/ThePredictions.Domain/Common/Enumerations/SeasonPassSource.cs` | Create | `Purchased`, `Trial` |
| `Competition` entity + `Competitions` table + admin page | Create (**Task 16**) | Stable competition reference data with logo + admin-editable API id — ADR 0017 |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/...` | Create | Tests for factory + flag |

## Implementation Steps

### Step 1: Add `RequiresPass` and prices to `Season`

- Add `public bool RequiresPass { get; private set; }` (default `false`).
- Add admin-set prices: `public decimal? EntryPrice { get; private set; }` and `public decimal? SmsPrice { get; private set; }` (full price of the +SMS tier). Null for free seasons.
- Add `public int CompetitionId { get; private set; }` (FK to the `Competitions` reference table, ADR 0017 / Task 16) — the **stable internal competition identity** used for the reward's same-competition match (Task 13) and comparable-season pricing (Task 15). **Remove both `ApiLeagueId` and `CompetitionType` from `Season`** — the provider id and competition type now live on `Competition` (resolved at sync time). The existing `Season.IsTournament` helper moves to read `Competition.Type` (update its callers — Task 16).
- Thread through the public constructor, `Create(...)`, and `UpdateDetails(...)` (default-false keeps all existing seasons free).
- Validation: when `RequiresPass` is `true`, require `EntryPrice > 0` and `SmsPrice >= EntryPrice`; when `false`, prices must be null. (Prices are set/edited in admin and suggested by the calculator — Task 15.)

### Step 2: Create enums

```csharp
public enum SeasonPassTier { Entry, EntryPlusSms }
public enum SeasonPassSource { Purchased, Trial }
```

(Competition is now a **reference entity/table**, not an enum — see Task 16 / ADR 0017.)

### Step 3: Create `SeasonPass`

```csharp
public class SeasonPass
{
    public int Id { get; init; }
    public int UserId { get; private set; }          // match existing user id type
    public int SeasonId { get; private set; }
    public SeasonPassTier Tier { get; private set; }
    public SeasonPassSource Source { get; private set; }
    public decimal AmountPaid { get; private set; }       // total paid for the pass
    public decimal SmsFeePaid { get; private set; }        // the SMS uplift actually paid (0 if Entry, trial, or comped)
    public string? StripePaymentReference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int SmsSentCount { get; private set; }          // SMS reminders sent to this user this season
    public int? RewardRedeemedForSeasonId { get; private set; }  // set when this pass's leftover funded a later free SMS season
    public bool SmsPaused { get; private set; }            // user paused their SMS for this season (in-app toggle)

    public bool HasSmsReminders => Tier == SeasonPassTier.EntryPlusSms;
    public bool ShouldSendSms => HasSmsReminders && !SmsPaused;

    public void RecordSmsSent()                            // called when an SMS reminder is sent
    {
        SmsSentCount++;
    }

    public void MarkRewardRedeemed(int redeemedForSeasonId)  // prevents reusing this pass's leftover twice
    {
        RewardRedeemedForSeasonId = redeemedForSeasonId;
    }

    public void PauseSms()  => SmsPaused = true;   // in-app toggle (no refund)
    public void ResumeSms() => SmsPaused = false;

    private SeasonPass() { }              // ORM — [ExcludeFromCodeCoverage]

    public SeasonPass(int id, int userId, int seasonId, SeasonPassTier tier, SeasonPassSource source,
        decimal amountPaid, decimal smsFeePaid, string? stripePaymentReference, DateTime createdAtUtc,
        int smsSentCount, int? rewardRedeemedForSeasonId) { /* hydrate */ }

    public static SeasonPass CreatePurchased(int userId, int seasonId, SeasonPassTier tier,
        decimal amountPaid, decimal smsFeePaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* validate + build */ }

    // Comped SMS upgrade earned via the early-bird reward: EntryPlusSms tier, smsFeePaid 0
    public static SeasonPass CreateRewardUpgrade(int userId, int seasonId,
        decimal amountPaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* EntryPlusSms, smsFeePaid 0 */ }

    // Free first-season trial: Entry comped. Optionally the user pays the SMS uplift on top.
    public static SeasonPass CreateTrial(int userId, int seasonId, IDateTimeProvider dateTimeProvider) { /* Entry tier, 0.00, Trial */ }

    public static SeasonPass CreateTrialWithSms(int userId, int seasonId, decimal smsFeePaid,
        string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* Source Trial, EntryPlusSms, AmountPaid = smsFeePaid (Entry comped) */ }
}
```

- Validate: `userId`/`seasonId` not default; purchased `amountPaid > 0` and reference not blank; `smsFeePaid >= 0` and only `> 0` when `Tier == EntryPlusSms`; trial = `Entry`, `0.00`, `Trial`. Use `Guard` clauses as elsewhere.
- Use `IDateTimeProvider` for `CreatedAtUtc` (never `DateTime.Now`).
- `SmsSentCount` starts at 0; `RecordSmsSent()` increments it (Task 11) and the reward (Task 13) reads it.
- **Reward modelling:** a comped (earned) SMS upgrade is `Tier = EntryPlusSms` with `SmsFeePaid = 0` (created via `CreateRewardUpgrade`); the pass that *funded* it is stamped via `MarkRewardRedeemed(...)` so its leftover can't be reused. (Whether to also add a distinct `SeasonPassSource.RewardUpgrade` value is a README open question.)

## Code Patterns to Follow

Mirror `Season.cs` / `League.cs`: private parameterless ctor for ORM (`[ExcludeFromCodeCoverage]`), public ctor for DB hydration, static `Create*` factories with validation.

## Verification

- [ ] Builds clean.
- [ ] Unit tests cover all factories (`CreatePurchased`, `CreateRewardUpgrade`, `CreateTrial`, `CreateTrialWithSms`), `HasSmsReminders`, `ShouldSendSms`, `RecordSmsSent()`, `MarkRewardRedeemed()`, `PauseSms()/ResumeSms()`, the `Season` price validation, and every guard branch.
- [ ] `coverage-unit.bat` shows **100% line + branch** on Domain.

## Edge Cases to Consider

- Trial pass must always be Entry tier, amount 0, `SmsFeePaid` 0, no Stripe reference.
- Purchased pass must reject zero amount / blank reference; `SmsFeePaid > 0` only allowed on `EntryPlusSms`.
- Reward-upgrade pass: `EntryPlusSms` tier but `SmsFeePaid = 0`.
- `Season` with `RequiresPass = true` must reject null/zero prices; `RequiresPass = false` must reject non-null prices.

## Notes

Confirm the existing user identifier type before finalising `UserId` (match `LeagueMember`).

`SeasonPass` is the per-(user, season) entitlement (effectively the "UserSeasonPass" record): one row per user per season, so overlapping/concurrent seasons each get their own pass. No separate table is needed for that.
