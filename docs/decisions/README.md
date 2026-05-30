# Decision Records (ADRs)

This folder holds **Decision Records** for ThePredictions — short documents capturing significant decisions, *why* they were made, what we weighed for and against, and what we rejected.

Although the format follows the **Architecture Decision Record (ADR)** convention, the remit here is broader than architecture: it also covers **product, business, legal, and financial** decisions, because for a small solo project those are just as consequential and their rationale is just as easy to forget.

## Why keep these

- Preserve the **reasoning**, not just the outcome — especially for legal/financial calls a future solicitor or accountant may need.
- Avoid re-litigating settled questions months later.
- Onboard future collaborators (or future-you) quickly.

## When to add one

Add a record when a decision is **hard to reverse, cross-cutting, or expensive to get wrong** — e.g. a monetisation model, a data/payment design, a legal stance, a business-structure choice. Don't bother for trivial or easily-reversed choices.

## Status lifecycle

`Proposed` → `Accepted` → (later) `Superseded by NNNN` / `Deprecated`. A `Deferred` status is used for decisions we've consciously parked (e.g. pending external advice).

## How to add one

1. Copy [`_template.md`](./_template.md) to `NNNN-short-title.md` (next number, zero-padded).
2. Fill it in; keep it short.
3. Add a row to the index below.
4. If a new decision reverses an old one, set the old one's status to `Superseded by NNNN` rather than editing its decision.

## Table of Contents

| # | Title | Status | Tags | Summary |
|---|-------|--------|------|---------|
| [0001](./0001-adopt-decision-records.md) | Adopt Decision Records (ADRs) | Accepted | process | Keep lightweight ADRs in `docs/decisions/` for product/business/legal/technical decisions. |
| [0002](./0002-monetise-via-one-off-season-passes.md) | Monetise via one-off Season Passes | Accepted | product, business, legal | Revenue = a one-off, non-recurring Season Pass per competition (software-access fee). |
| [0003](./0003-never-custody-prize-money.md) | Never custody or route prize money | Accepted / Deferred | legal, business | Prize money stays peer-to-peer; entry-fee routing deferred pending gambling-law advice. |
| [0004](./0004-operate-as-sole-trader.md) | Operate as a sole trader for launch | Accepted | business, financial, legal | Sole trader now (cost, loss relief); incorporate later at a trigger. |
| [0005](./0005-whole-site-pass-gating.md) | Whole-site pass gating | Accepted | product, legal | Pass needed for all leagues (free + money) for legal cleanliness. |
| [0006](./0006-free-season-trial-for-new-players.md) | Free season trial for new players | Accepted | product, business | First-ever participants get one free Entry season (growth + friction + fair transition). |
| [0007](./0007-world-cup-free-future-seasons-paid.md) | World Cup free; later seasons paid | Accepted | product, business, financial | WC costs are personal/sunk (never booked to business) → free; future seasons paid. |
| [0008](./0008-sms-as-a-season-pass-tier.md) | SMS as a season-pass tier | Accepted | product | Two products (Entry / Entry + SMS), one entitlement with an SMS flag. |
| [0009](./0009-sms-additive-transactional-no-stop.md) | SMS additive & transactional | Accepted | product, legal | SMS is extra (emails kept), final-window only, transactional, no inbound STOP, non-refundable. |
| [0010](./0010-self-funding-early-bird-reward.md) | Self-funding early-bird reward | Accepted | product, financial | Free next same-competition SMS season only when leftover fee covers that season's worst-case cost. |
| [0011](./0011-admin-configurable-season-pricing.md) | Admin-configurable pricing | Accepted | product, technical | Per-season Entry/SMS prices in DB, charged via dynamic Stripe amounts. |
| [0012](./0012-recommended-price-calculator.md) | Recommended-price calculator | Accepted | business, financial, product | Editable pre-filled suggestion: costs +15% buffer, length-weighted, break-even at last comparable season's players, min floor. |
| [0013](./0013-no-pay-to-win.md) | No pay-to-win monetisation | Accepted | product | Never sell competitive advantage; paid features are convenience/cosmetic only. |
| [0014](./0014-stripe-as-payment-processor.md) | Stripe payment processor | Accepted / Deferred | technical, business, legal | Stripe Checkout + wallets now; direct-charges Connect for future entry-fee routing. |
| [0015](./0015-defer-season-challenges.md) | Defer Season Challenges | Deferred | product | Badges/challenges parked as a separate future feature (cosmetic only). |
| [0016](./0016-seed-business-with-owner-capital.md) | Seed business with owner's capital | Accepted | business, financial | Float initial costs from personal money (capital introduced), repay via drawings once income covers them. |
