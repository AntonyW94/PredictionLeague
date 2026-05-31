# Task: Domain Model — Season Pass

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ✅ Phase A — buildable now (no accounts).

## Status

Not Started | In Progress | **Complete**

> Domain only: `Season` gained `PassStandardPrice`/`PassPremiumPrice` (with validation) plus a **computed** `RequiresPass => PassStandardPrice.HasValue` (no stored column - a season is pass-required exactly when it has prices), and `SeasonPass` + the `SeasonPassTier`/`SeasonPassSource` enums were added. The `SeasonPasses` **table** stays in Task 07 (no reads/writes until the access gate, Task 08); only the additive `Seasons` price columns are migrated (presented as SQL in chat, no committed file). Season create always makes a free season; update preserves existing pricing - admin pricing UI is Task 15.

## Goal

Add admin-set prices to `Season` (with `RequiresPass` derived from them), and introduce the `SeasonPass` domain entity (with SMS usage + reward tracking) and supporting enums.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Season.cs` | Modify | Add prices and `CompetitionId` (FK) plus a computed `RequiresPass`; **drop `ApiLeagueId` and `CompetitionType`** (both move to `Competitions`, ADR 0009) |
| `src/ThePredictions.Domain/Models/SeasonPass.cs` | Create | New entity |
| `src/ThePredictions.Domain/Common/Enumerations/SeasonPassTier.cs` | Create | `Standard`, `Premium` |
| `src/ThePredictions.Domain/Common/Enumerations/SeasonPassSource.cs` | Create | `Purchased`, `Trial`, `Free` (free-season participation) |
| `Competition` entity + `Competitions` table + admin page | Create (**Task 16**) | Stable competition reference data with logo + admin-editable API id — ADR 0009 |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/...` | Create | Tests for factory + flag |

## Implementation Steps

### Step 1: Add prices to `Season` (RequiresPass derived)

- Add a **computed** `public bool RequiresPass => PassStandardPrice.HasValue;` (no stored column - a season is pass-required exactly when it has prices).
- Add admin-set prices: `public decimal? PassStandardPrice { get; private set; }` and `public decimal? PassPremiumPrice { get; private set; }` (full price of the +SMS tier). Null for free seasons.
- Add `public int CompetitionId { get; private set; }` (FK to the `Competitions` reference table, ADR 0009 / Task 16) — the **stable internal competition identity** used for the reward's same-competition match (Task 13) and comparable-season pricing (Task 15). **Remove both `ApiLeagueId` and `CompetitionType` from `Season`** — the provider id and competition type now live on `Competition` (resolved at sync time). The existing `Season.IsTournament` helper moves to read `Competition.Type` (update its callers — Task 16).
- Thread through the public constructor, `Create(...)`, and `UpdateDetails(...)` (default-false keeps all existing seasons free).
- Validation: a season is either **free** (both prices null) or **paid** (both prices set, `PassStandardPrice > 0`, `PassPremiumPrice >= PassStandardPrice`); a mismatch (one set, one null) is rejected. (Prices are set/edited in admin and suggested by the calculator — Task 15.)

### Step 2: Create enums

```csharp
public enum SeasonPassTier { Standard, Premium }
public enum SeasonPassSource { Purchased, Trial, Free }   // Free = £0 record for free-season participation (burns the freebie)
```

(Competition is now a **reference entity/table**, not an enum — see Task 16 / ADR 0009.)

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
    public decimal SmsFeePaid { get; private set; }        // the SMS uplift actually paid (0 if Standard, trial, or comped)
    public string? StripePaymentReference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int SmsSentCount { get; private set; }          // SMS reminders sent to this user this season
    public int? RewardRedeemedForSeasonId { get; private set; }  // set when this pass's leftover funded a later free SMS season

    public bool HasSmsReminders => Tier >= SeasonPassTier.Premium;   // per-pass entitlement (SMS pause is a per-user setting, Task 11)

    public void RecordSmsSent()                            // called when an SMS reminder is sent
    {
        SmsSentCount++;
    }

    public void MarkRewardRedeemed(int redeemedForSeasonId)  // prevents reusing this pass's leftover twice
    {
        RewardRedeemedForSeasonId = redeemedForSeasonId;
    }

    private SeasonPass() { }              // ORM — [ExcludeFromCodeCoverage]

    public SeasonPass(int id, int userId, int seasonId, SeasonPassTier tier, SeasonPassSource source,
        decimal amountPaid, decimal smsFeePaid, string? stripePaymentReference, DateTime createdAtUtc,
        int smsSentCount, int? rewardRedeemedForSeasonId) { /* hydrate */ }

    public static SeasonPass CreatePurchased(int userId, int seasonId, SeasonPassTier tier,
        decimal amountPaid, decimal smsFeePaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* validate + build */ }

    // Comped SMS upgrade earned via the early-bird reward: Premium tier, smsFeePaid 0
    public static SeasonPass CreateRewardUpgrade(int userId, int seasonId,
        decimal amountPaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* Premium, smsFeePaid 0 */ }

    // Free first-season trial: Standard comped. Optionally the user pays the SMS uplift on top.
    public static SeasonPass CreateTrial(int userId, int seasonId, IDateTimeProvider dateTimeProvider) { /* Standard tier, 0.00, Trial */ }

    public static SeasonPass CreateTrialWithSms(int userId, int seasonId, decimal smsFeePaid,
        string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* Source Trial, Premium, AmountPaid = smsFeePaid (Standard comped) */ }

    // Free-season participation record: £0, Source Free, Standard tier — exists so free play burns the freebie (ADR 0005)
    public static SeasonPass CreateFree(int userId, int seasonId, IDateTimeProvider dateTimeProvider) { /* Standard, 0.00, Free */ }
}
```

- Validate: `userId`/`seasonId` not default; purchased `amountPaid > 0` and reference not blank; `smsFeePaid >= 0` and only `> 0` when `Tier == Premium`; trial = `Standard`, `0.00`, `Trial`. Use `Guard` clauses as elsewhere.
- Use `IDateTimeProvider` for `CreatedAtUtc` (never `DateTime.Now`).
- `SmsSentCount` starts at 0; `RecordSmsSent()` increments it (Task 11) and the reward (Task 13) reads it.
- **Reward modelling:** a comped (earned) SMS upgrade is `Tier = Premium` with `SmsFeePaid = 0` (created via `CreateRewardUpgrade`); the pass that *funded* it is stamped via `MarkRewardRedeemed(...)` so its leftover can't be reused. (Whether to also add a distinct `SeasonPassSource.RewardUpgrade` value is a README open question.)

## Code Patterns to Follow

Mirror `Season.cs` / `League.cs`: private parameterless ctor for ORM (`[ExcludeFromCodeCoverage]`), public ctor for DB hydration, static `Create*` factories with validation.

## Verification

- [ ] Builds clean.
- [ ] Unit tests cover all factories (`CreatePurchased`, `CreateRewardUpgrade`, `CreateTrial`, `CreateTrialWithSms`, `CreateFree`), `HasSmsReminders`, `RecordSmsSent()`, `MarkRewardRedeemed()`, the `Season` price validation, and every guard branch.
- [ ] `coverage-unit.bat` shows **100% line + branch** on Domain.

## Edge Cases to Consider

- Trial pass must always be Standard tier, amount 0, `SmsFeePaid` 0, no Stripe reference.
- Purchased pass must reject zero amount / blank reference; `SmsFeePaid > 0` only allowed on `Premium`.
- Reward-upgrade pass: `Premium` tier but `SmsFeePaid = 0`.
- A paid `Season` must reject null/zero prices or `PassPremiumPrice < PassStandardPrice`; a free season must reject a single price being set (both must be null).

## Notes

Confirm the existing user identifier type before finalising `UserId` (match `LeagueMember`).

`SeasonPass` is the per-(user, season) entitlement (effectively the "UserSeasonPass" record): one row per user per season, so overlapping/concurrent seasons each get their own pass. No separate table is needed for that.
