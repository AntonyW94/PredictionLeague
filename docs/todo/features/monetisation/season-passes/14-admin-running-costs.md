# Task: Admin Running Costs

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Give the admin a page to record the website's running costs — amount, frequency, renewal/expiry date, and who currently pays — so the recommended-price calculator (Task 15) bases suggestions on **real, business-borne** costs.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/RunningCost.cs` | Create | Entity |
| `src/ThePredictions.Domain/Common/Enumerations/CostFrequency.cs` | Create | `Monthly`, `Annual`, `OneOff` |
| `src/ThePredictions.Domain/Common/Enumerations/CostPayer.cs` | Create | `Business`, `PersonalUntilRenewal` |
| `src/ThePredictions.Application/Repositories/IRunningCostRepository.cs` (+ impl) | Create | CRUD (commands) |
| `...Application/Features/Admin/RunningCosts/Commands|Queries/*` | Create | Create/Update/Delete + list (queries via read connection) |
| `src/ThePredictions.Web.Client/Components/Pages/Admin/RunningCosts.razor` | Create | Admin page `/admin/running-costs` |
| DB table | Create | `RunningCosts` (defined in Task 07) |

## Implementation Steps

### Step 1: Domain entity

Per the owner's decision, capture **cost type, price, and start/end dates** so the cost can be apportioned/prorated **any way we may want in future** (ADR 0012):

```csharp
public class RunningCost
{
    public int Id { get; init; }
    public string Name { get; private set; }            // e.g. "Fasthosts hosting", "api-sports.io", "Brevo"
    public decimal Amount { get; private set; }          // the price
    public CostFrequency Frequency { get; private set; } // cost type: Monthly / Annual / OneOff
    public DateTime StartDateUtc { get; private set; }   // when this cost (period) begins
    public DateTime? EndDateUtc { get; private set; }    // when it ends / next renewal (null = ongoing)
    public CostPayer Payer { get; private set; }         // Business / PersonalUntilRenewal
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public decimal AnnualisedAmount => Frequency switch    // convenience for the calculator
    {
        CostFrequency.Monthly => Amount * 12,
        CostFrequency.Annual  => Amount,
        _                     => Amount                     // OneOff handled by date/horizon logic in Task 15
    };

    // Business bears this cost on/after the given date (personal costs move to the business at EndDate/renewal)
    public bool IsBusinessBorneOn(DateTime dateUtc)
        => Payer == CostPayer.Business
           || (EndDateUtc.HasValue && EndDateUtc.Value <= dateUtc);
}
```

Storing **start + end dates** (not just a single renewal date) lets the calculator prorate by date-overlap with a season later, while the simple `AnnualisedAmount`/`IsBusinessBorneOn` helpers serve the current approach. Factory + validation (name not blank, amount ≥ 0, `StartDateUtc` set, `EndDateUtc` ≥ `StartDateUtc` when present). `IDateTimeProvider` for `CreatedAtUtc`.

### Step 2: Admin CRUD page

- `/admin/running-costs` (admin-only): list, add, edit, delete costs.
- Columns: Name, Amount, Frequency, Renewal/Expiry, Payer, Notes.
- Make clear the **PersonalUntilRenewal** meaning: "you're paying this personally for now; it moves to the business — and into pricing — from the renewal date." (Matches owner's plan to migrate costs to the business bank as they renew.)

### Step 3: Feed the calculator

- Expose a query the calculator (Task 15) uses: business-borne annualised costs as at a target date (uses `IsBusinessBorneOn`).

## Code Patterns to Follow

- Commands → repositories; queries → `IApplicationReadDbConnection`.
- Admin page + state-service pattern per `src/ThePredictions.Web.Client/CLAUDE.md`.

## Verification

- [ ] Admin can add/edit/delete costs with frequency, renewal date, payer.
- [ ] `AnnualisedAmount` and `IsBusinessBorneOn` behave correctly (incl. personal-until-renewal excluded before renewal).
- [ ] Domain coverage 100%.

## Edge Cases to Consider

- One-off costs (e.g. a single tool purchase) — excluded from recurring apportionment; handled by horizon logic in Task 15.
- A personal cost whose renewal is in the past → already business-borne.
- Currency GBP throughout.

## Notes

Owner has covered some launch costs personally and won't charge them to the business until they renew — `PersonalUntilRenewal` + `RenewalDateUtc` models exactly that.
