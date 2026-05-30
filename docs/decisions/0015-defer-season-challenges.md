# 0015. Defer Season Challenges to a separate future feature

- **Status:** Deferred
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product

## Context

An idea arose for earnable, show-off **badges/challenges tied to a season pass** (e.g. "Early Bird", "Perfect Round"). It's appealing but sizeable, and depends on the Season Pass concept shipping first.

## Decision

**Defer** Season Challenges to a separate future feature, captured as a stub at `docs/todo/features/monetisation/season-challenges/`. It is **out of scope** for the Season Passes build.

## Consequences

**For / positive**
- Keeps the Season Passes scope focused and shippable.
- Captured now so the idea isn't lost.

**Against / cost**
- Players wait longer for gamification.

**Neutral / notes**
- Relates to the existing `user-experience/achievements-badges` feature — decide later whether to merge.
- Any challenge must stay **cosmetic/achievement only** per 0013 (no pay-to-win); "Early Bird" dovetails with the SMS reward (0010).

## Alternatives considered

- **Build it alongside Season Passes** — rejected; scope creep on a feature that needs to ship.

## Related

- 0010, 0013; `docs/todo/features/monetisation/season-challenges/`
