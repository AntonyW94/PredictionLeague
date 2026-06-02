# 0011. Dynamic prize pot & configurable prize scheme

- **Status:** Proposed
- **Date:** 2026-06-02
- **Deciders:** Antony (owner)
- **Tags:** product, financial, technical

## Context

Today a league's prizes are entered manually **after** the entry deadline
(`DefinePrizeStructureCommandHandler`): the handler blocks until
`EntryDeadlineUtc` has passed and requires the hand-entered amounts to equal the
pot **to the penny** (`totalAllocatedPrizes == Price × Members.Count`).

Two problems follow:
1. **Prospective members can't see what they'd be entering** — there is no
   breakdown before entries close, so they can't judge whether to join.
2. **Round numbers are impossible** — a hand-split that must total an arbitrary
   pot rarely lands on tidy figures.

We want the inverse: the admin configures a **scheme** up front (which prize
categories pay out, how each entry's money is split, which boosts are available),
and the system evaluates that scheme into a concrete, round-number breakdown
**live at any entrant count**, freezing it at the deadline.

Hard constraint: per **ADR-0003** the platform never holds or moves prize money.
This feature is **display + configuration only**; the existing
`Winnings` → `LeaguePayouts` peer-to-peer settlement flow (**ADR-0010**) is
unchanged. We only change how the *initial* `LeaguePrizeSettings` are produced.

The full design exploration, with options and worked examples, lives in
[`docs/todo/features/dynamic-prize-pot/`](../todo/features/dynamic-prize-pot/README.md).

## Decision

We will adopt a **block-apportionment** prize scheme (Option C) with these rules:

1. **£1 blocks.** The rounding unit is £1; stakes are whole pounds. Fine
   granularity so even a small stake can fund several categories.
2. **Per-entry allocation.** The admin splits the **pounds of each entry** across
   the enabled categories (must total the stake), via **pound sliders with a live
   derived-prize preview**. After `N` entrants a category's sub-pot is
   `perEntryPounds × N`.
3. **Places scale with entrants** via an admin-editable **threshold table**
   (default: 1 place ≤5 entrants, 2 at 6–10 [70/30], 3 at 11–20 [50/30/20], 4 at
   21–40 [50/25/15/10], 5 at 41–75, 6 at 76+).
4. **£5 rounding on placed prizes (Overall and Section)**, triggered **per
   individual prize**: when a category's top place would naturally pay **more than
   £5**, the whole category fund is handed out in clean £5 chunks (floored,
   top-down) so **every** placed prize is a clean £5, and the odd £1–£4 **spills
   into another category's fund** (priority Most Exact Scores → Overall → Section;
   fallback: the top place of the same category absorbs it). A Section spills its
   per-stage remainders the same way. Funds whose top place is **≤ £5 stay
   £1-granular** so small/early pots are not distorted. **Round, Monthly and Most
   Exact Scores prizes keep natural £1 values** and absorb the spillover cleanly.
   The spillover is **conserved and stateless** — recomputed from `(scheme, pot, N)`
   on every change, never stored, so it moves freely between funds during
   registration and can return; the total across all funds always equals the pot.
   Only the deadline freeze is permanent. (Grounded in last season's real prizes:
   £4/round, £48 exact, £25/month, £220/£120/£90 overall.)
5. **Category registry**, each entry declaring a default weight, a *kind*
   (`EndOfSeason`, `Recurring`, `Staged`) and an **availability gate**:
   - **Monthly → seasons only** (tournaments are too short); **Section →
     tournaments only**. Overall / Round / Most-Exact-Scores: all competitions.
   - Recurring (Round/Monthly): winner-takes-all per event, prize =
     `floor(subPot ÷ events)`.
   - Staged (Section): split 50/50 across the two stages, each ranked by the
     ladder.
   - Adding a future prize type = one registry row (+ an `IPrizeStrategy`).
6. **Write-once scheme.** Editable only while unset: new leagues set it at
   creation and it locks; existing schemeless leagues (the in-flight World Cup)
   can set it once via Edit, then lock. A **site admin** may override a locked
   scheme to correct mistakes; league admins cannot.
7. **Freeze at the deadline.** A handler converts the scheme + final pot into the
   existing `LeaguePrizeSettings` rows; settlement is unchanged.
8. **Surface to all three audiences**: admin & members see the live projected
   breakdown ("finalises at the deadline"); prospective members see it **plus the
   +£x effect of their own entry**, attributed ("your £25 adds £15 to overall…").

## Consequences

**For / positive**
- Prospective members can see the prize breakdown and their marginal impact
  before committing — the core goal.
- Prizes are always round, always sum exactly to the pot, and scale winners with
  entrants automatically.
- No money-handling change → stays clearly within ADR-0003.
- Pure-integer apportionment is easy to test to the required 100% coverage.

**Against / cost**
- Prizes constrained to £1 (and placed prizes to £5 once a place exceeds £5); no
  arbitrary bespoke amounts via the normal path.
- Per-rank deltas can be lumpy as blocks re-apportion (category-level deltas stay
  stable — that's the headline shown to joiners).
- Net-new build: scheme storage, evaluation engine, boost admin UI (none today),
  prospective preview, and a freeze step.
- Write-once removes self-service correction for league admins (mitigated by the
  site-admin override).

**Neutral / notes**
- Stakes must be whole pounds when prizes are on.
- The deadline lock in `DefinePrizeStructure` is superseded by the scheme +
  freeze; that handler is retained only as a site-admin manual override.

## Alternatives considered

- **Option A — percentages + central payout curve.** Simpler admin UX, but
  %→round-number snapping is fiddly on small pots and the headline prize wobbles
  on most joins. Kept as a fallback if "exact percentages, not tied to round
  blocks" ever matters more than clean round numbers.
- **Option B — admin-authored rule tiers.** Maximum flexibility but a heavy
  rule-builder UI, easy to misconfigure, and the hardest place to guarantee round
  numbers. Its threshold idea is reused as the places table.
- **£5 blocks everywhere.** Guaranteed every prize £5-clean and exact, but too
  coarse — a £10 stake yields only 2 blocks and can't fund three categories.
  Rejected in favour of £1 blocks + overall-only £5 rounding.

## Related

- Feature plan & worked examples: [`docs/todo/features/dynamic-prize-pot/`](../todo/features/dynamic-prize-pot/README.md)
- [ADR-0003](./0003-no-custody-of-prize-money.md) — no custody of prize money (unaffected).
- [ADR-0010](./0010-entry-fee-settlement.md) — peer-to-peer settlement (settlement flow preserved).
