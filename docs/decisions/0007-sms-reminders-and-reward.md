# 0007. SMS reminders & early-bird reward

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, legal, financial

## Context

SMS deadline reminders are a paid extra over the free email reminders. SMS costs real money per message (Brevo), so it must be priced to clear cost, sent sparingly, comply with UK rules, and ideally reward the behaviour we want (early submission).

## Decision

### a) Sold as a season-pass tier
Two products per pass-required season — **"Entry"** and **"Entry + SMS"** — chosen at a single checkout. Modelled as **one `SeasonPass` with an SMS flag** (`Tier = Entry | EntryPlusSms`), two dynamic price points.

### b) Additive, final-window, transactional-only, UK mobiles
- **Additive:** everyone keeps **all** email reminders at every milestone; SMS-tier holders get an **extra** text **only at the final window (6h and 1h)** and **only if still unsubmitted**.
- Content is **strictly transactional** (deadline only) → **no legal opt-out required** (PECR). **No inbound STOP**; instead an in-app **"pause SMS" toggle** (in scope for v1).
- Refunds follow the pass rule (0005); pausing is **not** a refund.
- **UK mobiles only**, **required and validated at purchase** via `libphonenumber-csharp` (region GB, type Mobile), stored as **E.164**. Non-UK numbers aren't offered SMS.
- Final-window milestones = **2** (6h + 1h), configurable.

### c) Self-funding early-bird reward
A free SMS upgrade for the **next season of the same competition** (matched on `Season.CompetitionId`, 0009), but only when **provably profitable**. For a paying SMS pass **X** (`SmsFeePaid > 0`, not yet redeemed):

```
ppm        = current price per SMS message
leftover_X = X.SmsFeePaid − (X.SmsSentCount × ppm)
worstCase  = X.season.rounds × finalWindowMilestones(2) × ppm   # assume next season ≈ X's length
eligible   = leftover_X ≥ worstCase   → next same-competition season's SMS is free
```

Same competition only (the next season may not exist yet, so we assume the current season's length). A comped season pays no fee, so it can't fund another free one. Shown in-app as a **remaining SMS budget** to protect, not a filling bar.

## Consequences

**For / positive**
- One purchase/transaction/entitlement; SMS subscribers lose nothing (emails still arrive).
- Transactional-only = no two-way SMS plumbing/cost; early submitters get zero texts (low cost) and the reward reinforces early submission.
- The reward is guaranteed never to lose money and varies naturally with season length and live SMS cost.

**Against / cost**
- Must keep SMS content disciplined (any promo line flips it to marketing → opt-out required).
- Reward logic needs the live per-message rate at purchase; tracked via `SmsFeePaid`, `SmsSentCount`, `RewardRedeemedForSeasonId` on `SeasonPass`.
- No mid-season "add SMS later" path in v1.

## Alternatives considered

- **Separate SMS add-on purchase** — rejected; wastes a fixed Stripe fee and adds states.
- **Three tiers (incl. insights)** — deferred; fragments a tiny audience.
- **SMS replaces the email / SMS at all milestones / inbound STOP** — rejected; subscribers shouldn't lose emails, all-milestone is spammy/costly, STOP isn't required for transactional SMS.
- **Fixed reward threshold (e.g. <10)** — rejected; ignores season length and real cost.
- **Any-next-competition reward / filling "progress" bar** — rejected; next length unknown, and each SMS works *against* the reward (a depleting budget is the right model).

## Related

- 0002, 0005, 0006, 0009; `season-passes/04-brevo-sms-setup.md`, `10-purchase-page.md`, `11-sms-reminders.md`, `13-sms-earned-upgrade.md`.
