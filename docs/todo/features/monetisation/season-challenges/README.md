# Feature: Season Challenges (Badges) — STUB / Future

## Status

**Not Started** (stub — captured for later, out of scope for Season Passes)

> **Verified in code (2026-07-25):** a general badges/achievements engine is
> already **built and complete** in code (`BadgeCatalogue`, `UserBadgeRepository`,
> `EvaluateBadgesForRoundCommandHandler`, `BackfillBadgesCommand`) - its plan was
> completed and removed. It already awards the exact badges this stub names
> (e.g. "Sharpshooter"/Perfect Round, "Marksman"/season exact scores, "Ever-Present").
> The **only** part of *this* feature still outstanding is gating/scoping badges to a
> **paid Season Pass**. Treat this as "badges done; pass-gating not started" rather
> than a greenfield build.

## Summary

Earnable, show-off **badges/challenges tied to a season pass** — e.g. *"Early Bird"* (predicted before the 6-hour mark every round), *"Perfect Round"* (all exact scores in a round), *"Exact Machine"* (N exact scores in a season), *"Ever-Present"* (predicted every round). Players collect them on their season profile and show them off in leagues.

## User Story

As a player, I want to earn badges for my predictions and habits during a season so that I have extra goals and bragging rights beyond the leaderboard.

## Why It's a Separate Feature

- It depends on the **Season Pass** concept existing first (badges hang off a pass/season).
- It's a sizeable gamification surface (definitions, awarding engine, display, notifications) — best planned on its own.
- It relates closely to the existing achievements-badges feature (now shipped, plan removed); decide whether to merge or keep season-scoped challenges distinct.

## Synergy with Season Passes

- An **"Early Bird"** challenge dovetails with the SMS early-bird reward (Task 13) — both reward getting predictions in early.
- Badges could be a perk that adds value to the paid pass without being pay-to-win (purely cosmetic/achievement, never affecting points).

## Out of Scope For Now

Not part of the Season Passes build. Flesh this out after passes ship. Keep any challenge that touches scoring **cosmetic only** — no purchasable advantage (consistent with the no-pay-to-win principle).
