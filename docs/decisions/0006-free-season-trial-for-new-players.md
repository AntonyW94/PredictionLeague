# 0006. One free season trial for first-time players

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business

## Context

Whole-site gating (0005) removes the casual free on-ramp in paid seasons. We need a way to let genuinely new players experience the product before paying, without giving freebies to existing/returning players.

## Decision

Every user who has **never participated before** (no approved league membership in any season and no prior pass) is **auto-granted a one-time free Entry-tier Season Pass** the first time they try to take part in a pass-required season. The trial is **once per user, lifetime, Entry tier only** (no free SMS). Participating in a free season (e.g. World Cup) counts as participation and therefore consumes "new player" status.

## Consequences

**For / positive**
- **Word-of-mouth growth** — new invitees can join a friend's league free.
- **Lower signup friction** — try-before-you-buy.
- **Fair transition to paid** — genuine newcomers aren't hit with an immediate paywall.

**Against / cost**
- One season's access given away per new user (cost is near-zero for free leagues; modest for paid).
- Someone could use the free World Cup as their "trial" and then pay for PL — accepted and intended (free-season participation consumes the newcomer trial).

**Neutral / notes**
- Implemented as branch 3 of the access rule; trial pass recorded with `Source = Trial`.

## Alternatives considered

- **Discount instead of free** — rejected; a free first season is a stronger, simpler hook.
- **No trial** — rejected; too much signup friction given 0005.

## Related

- 0002, 0005, 0007; `season-passes/08-access-gate-and-trial.md`
