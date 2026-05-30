# 0021. No late entry — entries close when a season starts

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product

## Context

For a pass-required season, we need a clear cut-off for buying a pass and joining. An earlier note floated "late-join pricing" (joining mid-season at full price); the owner has confirmed that is **not** wanted.

## Decision

There is **no late entry and no late/pro-rata pricing**. Paid seasons simply **inherit the existing per-league entry-deadline rules** that already prevent joining a free season once entry has closed — no new mechanism is added. The only extra is that the purchase page **won't offer a pass once entry for that season has closed**. The refund window (0019) uses the season's first round deadline as its cut-off.

## Consequences

**For / positive**
- Simple, unambiguous rule; one cut-off shared with refunds (0019).
- No need for pro-rata/late pricing logic.
- Fair: everyone in a season started together.

**Against / cost**
- Someone who misses the start must wait for the next season — accepted.

**Neutral / notes**
- **No access-gate change needed** — existing entry-deadline rules already cover this for free and paid seasons alike. The purchase page just hides purchase once entry has closed.

## Alternatives considered

- **Late-join (full price, mid-season)** — rejected by the owner.
- **Pro-rata late pricing** — rejected; needless complexity for an unwanted behaviour.
- **A new season-level "entry closed" exception** — rejected; redundant with the existing per-league entry deadlines.

## Related

- 0019 (refund cut-off); `season-passes/10-purchase-page.md`
