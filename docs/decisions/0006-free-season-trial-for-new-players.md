# 0006. One free season trial for first-time players

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, business

## Context

Whole-site gating (0005) removes the casual free on-ramp in paid seasons. We need a way to let genuinely new players experience the product before paying, without giving freebies to existing/returning players.

## Decision

Every user who has **never participated before** (no approved league membership in any season and no prior pass) is **auto-granted a one-time free Entry-tier Season Pass** the first time they try to take part in a pass-required season. The **Entry portion is free**; if the new user wants SMS, they may **pay just the SMS uplift** on top of the free trial (so they're not denied SMS, but the trial only comps Entry). The trial is **once per user, lifetime**. Participating in a free season (e.g. World Cup) counts as participation and therefore consumes "new player" status.

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

- **Trial includes free SMS** — rejected; SMS has a real per-message cost, so the trial comps Entry only and the user pays the uplift if they want texts.
- **Trial is Entry-only with no SMS option** — rejected; we don't want to block a keen new user from SMS, just from getting it free.
- **Discount instead of free** — rejected; a free first season is a stronger, simpler hook.
- **No trial** — rejected; too much signup friction given 0005.

## Related

- 0002, 0005, 0007; `season-passes/08-access-gate-and-trial.md`
