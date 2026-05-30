# Task: Domain Model — Season Pass

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the `RequiresPass` flag to `Season` and introduce the `SeasonPass` domain entity with supporting enums.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Season.cs` | Modify | Add `RequiresPass` flag (default false) |
| `src/ThePredictions.Domain/Models/SeasonPass.cs` | Create | New entity |
| `src/ThePredictions.Domain/Common/Enumerations/SeasonPassTier.cs` | Create | `Entry`, `EntryPlusSms` |
| `src/ThePredictions.Domain/Common/Enumerations/SeasonPassSource.cs` | Create | `Purchased`, `Trial` |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/...` | Create | Tests for factory + flag |

## Implementation Steps

### Step 1: Add `RequiresPass` to `Season`

- Add `public bool RequiresPass { get; private set; }` (default `false`).
- Thread through the public constructor, `Create(...)`, and `UpdateDetails(...)` (default-false keeps all existing seasons free).

### Step 2: Create enums

```csharp
public enum SeasonPassTier { Entry, EntryPlusSms }
public enum SeasonPassSource { Purchased, Trial }
```

### Step 3: Create `SeasonPass`

```csharp
public class SeasonPass
{
    public int Id { get; init; }
    public int UserId { get; private set; }          // match existing user id type
    public int SeasonId { get; private set; }
    public SeasonPassTier Tier { get; private set; }
    public SeasonPassSource Source { get; private set; }
    public decimal AmountPaid { get; private set; }
    public string? StripePaymentReference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int SmsSentCount { get; private set; }        // SMS reminders sent to this user this season

    public bool HasSmsReminders => Tier == SeasonPassTier.EntryPlusSms;

    public void RecordSmsSent()                          // called when an SMS reminder is sent
    {
        SmsSentCount++;
    }

    private SeasonPass() { }              // ORM — [ExcludeFromCodeCoverage]

    public SeasonPass(int id, int userId, int seasonId, SeasonPassTier tier,
        SeasonPassSource source, decimal amountPaid, string? stripePaymentReference, DateTime createdAtUtc) { /* hydrate */ }

    public static SeasonPass CreatePurchased(int userId, int seasonId, SeasonPassTier tier,
        decimal amountPaid, string stripePaymentReference, IDateTimeProvider dateTimeProvider) { /* validate + build */ }

    public static SeasonPass CreateTrial(int userId, int seasonId, IDateTimeProvider dateTimeProvider) { /* Entry tier, 0.00, Trial */ }
}
```

- Validate: `userId`/`seasonId` not default; purchased `amountPaid > 0` and reference not blank; trial = `Entry`, `0.00`, `Trial`. Use `Guard` clauses as elsewhere.
- Use `IDateTimeProvider` for `CreatedAtUtc` (never `DateTime.Now`).
- `SmsSentCount` starts at 0; `RecordSmsSent()` increments it (used by Task 11 and consumed by the reward in Task 13).
- **Reward modelling (for Task 13):** a free SMS upgrade earned by an early bird should result in a pass with `Tier = EntryPlusSms` while `AmountPaid` reflects only the Entry price paid. Decide whether to add a `SeasonPassSource.RewardUpgrade` value or a separate `SmsComped` flag — see README open question. Cover whichever is chosen with tests.

## Code Patterns to Follow

Mirror `Season.cs` / `League.cs`: private parameterless ctor for ORM (`[ExcludeFromCodeCoverage]`), public ctor for DB hydration, static `Create*` factories with validation.

## Verification

- [ ] Builds clean.
- [ ] Unit tests cover both factories, `HasSmsReminders`, `RecordSmsSent()`, and every guard branch.
- [ ] `coverage-unit.bat` shows **100% line + branch** on Domain.

## Edge Cases to Consider

- Trial pass must always be Entry tier, amount 0, no Stripe reference.
- Purchased pass must reject zero amount / blank reference.

## Notes

Confirm the existing user identifier type before finalising `UserId` (match `LeagueMember`).
