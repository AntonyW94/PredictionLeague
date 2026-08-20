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

**Supersede only established decisions.** A record is only "superseded" once it has actually **taken effect** (merged to the main branch / implemented). While a decision is still being drafted in a feature branch and has not yet taken effect, **edit the record in place** — don't create a supersede chain for a decision that never went live.

## How to add one

1. Copy [`_template.md`](./_template.md) to `NNNN-short-title.md` (next number, zero-padded).
2. Fill it in; keep it short.
3. Add a row to the index below.
4. If a new decision reverses an **already-established** one (merged/in effect), add a new record and set the old one's status to `Superseded by NNNN` rather than editing it. If the old one never took effect (still being drafted in this branch), just **edit it in place**.

## Table of Contents

Records are **thematic** — each groups a cohesive area of decisions (see 0001 for why).

| # | Title | Status | Tags | Covers |
|---|-------|--------|------|--------|
| [0001](./0001-decision-records-process.md) | Decision Records process | Accepted | process | Why/how we keep thematic ADRs; supersede policy. |
| [0002](./0002-monetisation-model.md) | Monetisation model & non-gambling stance | Accepted | product, business, legal | One-off Season Passes; whole-site gating; no pay-to-win. |
| [0003](./0003-no-custody-of-prize-money.md) | No custody of prize money | Accepted / Deferred | legal, business | Never hold/route prize money; entry-fee routing deferred pending gambling-law advice. |
| [0004](./0004-business-structure-and-funding.md) | Business structure & funding | Accepted | business, financial, legal | Sole trader for launch; seed with owner's capital, repay via drawings. |
| [0005](./0005-season-pass-lifecycle.md) | Season pass lifecycle | Accepted | product, business, financial | Which seasons are paid (WC free); free-first trial (free play burns it); refunds; no late entry. |
| [0006](./0006-season-pricing.md) | Season pricing | Accepted | product, business, financial, technical | Admin-configurable prices via dynamic Stripe; recommended-price calculator. |
| [0007](./0007-sms-reminders-and-reward.md) | SMS reminders & early-bird reward | Accepted | product, legal, financial | SMS tier; additive/transactional/UK-only; self-funding free-upgrade reward. |
| [0008](./0008-payments-stripe.md) | Payments (Stripe) | Accepted / Deferred | technical, business, legal | Stripe Checkout + wallets; direct-charges Connect for future entry-fee routing. |
| [0009](./0009-platform-and-data.md) | Platform & data | Accepted / Deferred | technical, domain, security | Competitions reference table; verified/normalised email; Season Challenges deferred. |
| [0010](./0010-entry-fee-settlement.md) | Peer-to-peer money settlement (entry fees & payouts) | Accepted | product, security, legal | Encrypted admin & player bank details; pay-on-join with admin accept; end-of-league payouts list with manual mark-as-paid. Software never touches the money. |
| [0011](./0011-dynamic-prize-pot.md) | Dynamic prize pot & configurable prize scheme | Proposed | product, financial, technical | Live, round-number prize breakdown from a write-once scheme (£1 blocks, overall-only £5 rounding, places scale with entrants); admin-configurable categories & boosts; prospective members see their +£x impact. Display/config only — no money moves. |
| [0012](./0012-email-gate-temporary-suspension.md) | Temporarily suspend the email-confirmation gate | Superseded by 0014 | product, technical, security | Removes the unconfirmed-email block on season-pass acquisition until verification emails ship, so new sign-ups aren't locked out. Amends 0009(b). |
| [0013](./0013-database-migrations-dbup.md) | Database migrations with DbUp | Accepted | technical | DbUp migration set under `Persistence.SqlServer/Migrations/` (scoped exception to rule #10); idempotent baseline from live prod; reusable `migrate-shared.yml` + per-env wrappers; forward-only rollback. |
| [0014](./0014-email-gate-re-enabled.md) | Re-enable the email-confirmation gate | Accepted | product, technical, security | Restores the confirmed-email requirement on taking part (acquire + checkout) now that verification emails ship; grandfathers existing users. Supersedes 0012. |
| [0015](./0015-cache-my-leagues-ranks.md) | Cache the My Leagues ranks on the write path | Accepted | technical | Moves every tile rank into `LeagueMemberStats` behind one idempotent recompute (no snapshots, absent = NULL, one metric per column); the read becomes a keyed lookup. Fixes a ~400ms-per-request query-plan compile. |
| [0016](./0016-business-rule-exception-classification.md) | Business rules get their own exception type | Accepted | technical | Business rules throw `BusinessRuleViolationException` (400; severity revised to Information by 0018); `InvalidOperationException` is no longer caught and means a server fault (500 + Error). Inverts a fail-open default that hid a broken leaderboard and 18 config faults in the client-error bucket. |
| [0017](./0017-sql-belongs-to-the-persistence-adapter.md) | SQL belongs to the persistence adapter | Accepted | technical | One interface per query, owned by Application and implemented in `Persistence.SqlServer`; every predicate classified as a rule or a fetch, rules moved to C# with tests, adapter-neutral conformance suite. 331 `SELECT`s out of Application, 59 coverage exclusions to none, nine duplicated rules collapsed. Enforced by `SqlOwnershipConventionTests`. |
| [0018](./0018-log-severity-says-who-must-act.md) | Log severity says who has to act | Accepted | technical | Client faults (wrong id, failed validation, business rule, unconfirmed email, unauthorised) log at Information; Warning is reserved for what somebody must act on (slow query, missing index, failing third party); Error stays the unclassified-exception default from 0016. Nine middleware branches and `ValidationBehaviour` moved off Warning, which is what makes the zero-threshold warnings monitor readable. |
