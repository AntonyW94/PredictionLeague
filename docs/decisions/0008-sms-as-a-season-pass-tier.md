# 0008. Sell SMS reminders as a season-pass tier

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product

## Context

SMS deadline reminders are a paid extra over the free email reminders. This could be sold as a separate à-la-carte add-on purchase, or as a second pass tier. SMS costs real money per message (Brevo), so it must be priced to clear cost.

## Decision

Offer **two products per pass-required season — "Entry" and "Entry + SMS"** — chosen at a single checkout. Underneath, it's modelled as **one `SeasonPass` entitlement with an SMS flag** (`Tier = Entry | EntryPlusSms`), backed by two dynamic price points.

## Consequences

**For / positive**
- One purchase decision, one transaction, one entitlement — less state, fewer Stripe fixed fees than a separate add-on purchase.
- Single "do they have access / SMS this season?" check.

**Against / cost**
- No mid-season "add SMS later" path in v1 (can add a cheap upgrade SKU later if requested).

**Neutral / notes**
- Reminder job filters SMS recipients by the pass's SMS flag (see 0009).
- Per-season SMS usage is tracked to power the reward (0010).

## Alternatives considered

- **Separate SMS add-on purchase** — rejected; second transaction wastes a fixed Stripe fee on a small amount and adds states; "buy later" benefit not worth it for v1.
- **Three tiers (incl. insights)** — deferred; fragments a tiny audience.

## Related

- 0009, 0010, 0011; `season-passes/11-sms-reminders.md`
