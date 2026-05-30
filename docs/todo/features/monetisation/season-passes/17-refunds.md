# Task: Season Pass Refunds

**Parent Feature:** [README.md](./README.md)

> **Readiness:** ⛔ Phase B — needs Stripe (refund API).

## Status

**Not Started** | In Progress | Complete

## Goal

Allow a season pass (incl. any SMS uplift) to be refunded **before the season starts** (change of mind, or the unlikely season cancellation), via Stripe, revoking the entitlement. See ADR 0005.

## Policy (ADR 0005)

- **Refundable until the season's first round deadline.** After the season starts → non-refundable.
- Refund reverses the `SeasonPass` (entitlement revoked, can no longer take part).
- A refunded pass must **not** count toward the early-bird reward (its `SmsFeePaid` is treated as reversed) — see Task 13 / ADR 0007.
- Season cancellation → refund affected paid users regardless of start.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `...Infrastructure/Services/Payments/StripePaymentService.cs` | Modify | `RefundAsync(paymentReference)` via Stripe Refunds API |
| `...Application/Features/...Passes/Commands/RefundSeasonPassCommand(.Handler).cs` | Create | Validate window, refund, revoke entitlement |
| `src/ThePredictions.Domain/Models/SeasonPass.cs` | Modify | Add a refunded marker + `Refund()` method |
| `Seasons`/`SeasonPasses` schema | Modify | `SeasonPasses.RefundedAtUtc` (Task 07) |
| Admin/user UI | Create | "Cancel & refund" (self, pre-start) and an admin refund action |

## Implementation Steps

### Step 1: Domain

- Add `public DateTime? RefundedAtUtc { get; private set; }` and `public bool IsRefunded => RefundedAtUtc.HasValue;` to `SeasonPass`.
- `Refund(IDateTimeProvider)` sets `RefundedAtUtc`; guard against double-refund.
- A refunded pass is ignored by the access gate (Task 08) and by reward eligibility (Task 13).

### Step 2: Command

- `RefundSeasonPassCommandHandler`:
  - Load the pass + its season; **reject if the season has started** (first round deadline passed) unless an admin override flag for cancellation.
  - Call `StripePaymentService.RefundAsync(pass.StripePaymentReference)` for the amount paid.
  - `pass.Refund(...)`; persist via repository.
  - Log: `"Season pass (ID: {SeasonPassId}) refunded for user (ID: {UserId})"`.

### Step 3: UI

- Self-service: a "Cancel & refund" button on the user's pass while the season hasn't started.
- Admin: a refund action (also used for the cancellation scenario).

## Verification

- [ ] Pre-start refund succeeds, money returned via Stripe, entitlement revoked (user can no longer take part).
- [ ] Post-start refund is blocked (except admin cancellation override).
- [ ] Double-refund guarded.
- [ ] Refunded pass excluded from reward eligibility and the access gate.
- [ ] Domain coverage 100% (`Refund`, `IsRefunded`).

## Edge Cases to Consider

- Trial pass (no payment) — nothing to refund; "leaving" just removes participation.
- Reward-comped SMS upgrade (`SmsFeePaid = 0`) — refund only the amount actually paid.
- Stripe refund failure (already refunded/disputed) — surface and don't mark refunded.

## Related

- ADR 0005; Tasks 07, 08, 09, 13, 05 (Terms refund clause).
