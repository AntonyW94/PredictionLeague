# Task: Stripe Checkout Integration

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Let a user buy a Season Pass via a one-off Stripe Checkout session, and create the `SeasonPass` reliably on payment via webhook.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Infrastructure/Services/Payments/IPaymentService.cs` | Create | Abstraction over Stripe |
| `...Payments/StripePaymentService.cs` | Create | Creates Checkout sessions, verifies webhooks |
| `...Application/Features/...Passes/Commands/CreateCheckoutSessionCommand(.Handler).cs` | Create | Build session for (season, tier) |
| `...Application/Features/...Passes/Commands/FulfilSeasonPassCommand(.Handler).cs` | Create | Create `SeasonPass` from a paid session |
| `src/ThePredictions.API/Controllers/StripeWebhookController.cs` | Create | Receives `checkout.session.completed` |
| Config (Key Vault references) | Modify | Stripe keys, webhook secret, Price ID map |

## Implementation Steps

### Step 1: Config

- Stripe **secret key** + **webhook signing secret** from **Key Vault** (never `appsettings.json`).
- **No Price-ID map** — amounts come from the season's admin-set `EntryPrice` / `SmsPrice` (Tasks 06–07, 15), passed to Stripe as dynamic `price_data`.

### Step 2: Create Checkout session

- `StripePaymentService.CreateCheckoutSessionAsync(userId, seasonId, tier)`:
  - Look up the season's price for the chosen tier (`EntryPrice` for Entry, `SmsPrice` for Entry + SMS). If the user is **reward-eligible** (Task 13), the SMS tier is offered at `EntryPrice` (uplift comped).
  - Mode = **`payment`** (one-off).
  - Line item = dynamic **`price_data`** in GBP with `unit_amount` = the DB price (in pence). No pre-created Price object.
  - `client_reference_id` / `metadata` = `userId`, `seasonId`, `tier`, and the computed `smsFeePaid` (so the webhook can fulfil and record the SMS uplift paid).
  - `success_url` / `cancel_url` back to the site.
  - Card + wallets (Apple/Google Pay on by default in Checkout).
- Command returns the session URL; the Blazor page (Task 10) redirects to it.

### Step 3: Webhook fulfilment

- `StripeWebhookController` reads the raw body, **verifies the signature** with the webhook secret, handles **`checkout.session.completed`**.
- Dispatch `FulfilSeasonPassCommand(userId, seasonId, tier, amountPaid, smsFeePaid, paymentReference)`:
  - Idempotent: if a pass already exists for `(userId, seasonId)`, no-op (handles webhook retries + the unique index).
  - Else create the pass: `SeasonPass.CreatePurchased(...)`, or `CreateRewardUpgrade(...)` when the SMS uplift was comped — and if a reward funded it, `MarkRewardRedeemed(seasonId)` on the funding pass (Task 13).
  - Log: `"Season pass purchased for user (ID: {UserId}) in season (ID: {SeasonId})"`.

## Code Patterns to Follow

- Commands write via **repositories**.
- Keep Stripe SDK usage inside Infrastructure behind `IPaymentService`.
- API controller conventions per `src/ThePredictions.API/CLAUDE.md`.

## Verification

- [ ] Test-mode purchase creates exactly one `SeasonPass` with correct tier/amount/`SmsFeePaid`/reference.
- [ ] Checkout charges the season's current admin-set price (changing it in admin changes the charge, no Stripe edit).
- [ ] Webhook signature verified; bad signatures rejected.
- [ ] Duplicate/retry webhooks do not create duplicate passes (idempotent).
- [ ] Apple/Google Pay visible in the test Checkout.

## Edge Cases to Consider

- Payment succeeds but user closes the success page — pass still created (webhook is source of truth, not the redirect).
- Trial-eligible users must never reach Checkout (purchase page shows the free state instead).
- Currency = GBP; amount stored matches the Price.

## Notes

Webhook (server→server) is the authoritative fulfilment path. Never grant the pass purely on the `success_url` redirect.
