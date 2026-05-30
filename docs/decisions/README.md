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

## Index

| # | Title | Status |
|---|-------|--------|
| [0001](./0001-adopt-decision-records.md) | Adopt Decision Records (ADRs) | Accepted |
| [0002](./0002-monetise-via-one-off-season-passes.md) | Monetise via one-off Season Passes | Accepted |
| [0003](./0003-never-custody-prize-money.md) | Never custody or route prize money (defer entry-fee routing) | Accepted / Deferred |
| [0004](./0004-operate-as-sole-trader.md) | Operate as a sole trader for launch | Accepted |
| [0005](./0005-whole-site-pass-gating.md) | Season Pass gates the whole site, not just money leagues | Accepted |
| [0006](./0006-free-season-trial-for-new-players.md) | One free season trial for first-time players | Accepted |
| [0007](./0007-world-cup-free-future-seasons-paid.md) | World Cup 2026 free; existing seasons grandfathered; later seasons paid | Accepted |
| [0008](./0008-sms-as-a-season-pass-tier.md) | Sell SMS reminders as a season-pass tier | Accepted |
| [0009](./0009-sms-additive-transactional-no-stop.md) | SMS additive, final-window, transactional-only, no inbound STOP | Accepted |
| [0010](./0010-self-funding-early-bird-reward.md) | Self-funding, profit-based early-bird SMS reward | Accepted |
| [0011](./0011-admin-configurable-season-pricing.md) | Admin-configurable per-season pricing via dynamic Stripe amounts | Accepted |
| [0012](./0012-recommended-price-calculator.md) | Recommended-price calculator rules | Accepted |
| [0013](./0013-no-pay-to-win.md) | No pay-to-win monetisation | Accepted |
| [0014](./0014-stripe-as-payment-processor.md) | Stripe as payment processor; Apple/Google Pay; direct charges for future Connect | Accepted / Deferred |
| [0015](./0015-defer-season-challenges.md) | Defer Season Challenges to a separate future feature | Deferred |
