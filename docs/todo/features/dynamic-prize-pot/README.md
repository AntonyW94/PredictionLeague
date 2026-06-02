# Feature: Live Prize Pot & Configurable Prize Scheme

## Status

**Not Started** (design proposal — options laid out for a decision)

> This is a **thinking document**. It presents the problem, the constraints
> already baked into the code, and **three candidate designs with pros and
> cons**, plus a recommendation. Nothing here is built yet. Pick a direction
> and we'll turn the chosen option into an ADR + task breakdown.

## Summary

Today prizes are typed in by hand **after** the entry deadline and must sum to
the pot to the penny. We want the opposite: a prize **scheme** the admin
configures up front (which categories pay out, which boosts are available),
which the system can evaluate into a concrete, round-number breakdown **at any
time** against the current pot — so prospective members can see exactly what
they'd be entering, including the marginal effect of their own entry fee.

## User Story

- **As a league admin**, I want to choose which prize categories (Overall,
  Round, Section, Most Exact Scores) and which boosts apply to my league when I
  create/edit it, so the rules are set before anyone joins.
- **As a current member**, I want to see the live projected prize breakdown as
  more people join, so I know what I'm playing for.
- **As a prospective member**, I want to see the current breakdown *and what my
  entry fee would add to it* before I commit, so I can decide whether to enter.

## The core problem (and what's blocking it today)

Two rules in `DefinePrizeStructureCommandHandler` are the crux:

```csharp
// src/.../Leagues/Commands/DefinePrizeStructureCommandHandler.cs:28
if (league.EntryDeadlineUtc > dateTimeProvider.UtcNow)
    throw new InvalidOperationException("...cannot be defined until after the entry deadline...");

// :31-35  — prizes are hand-entered amounts that MUST equal the pot exactly
var totalPrizePot = league.Price * league.Members.Count;
var totalAllocatedPrizes = request.PrizeSettings.Sum(p => p.PrizeAmount * p.Multiplier);
if (totalAllocatedPrizes != totalPrizePot)
    throw new InvalidOperationException("...must equal the total prize pot.");
```

1. **Deadline lock** → there is *no* breakdown to show before entries close.
2. **Exact-penny invariant** → "round numbers" are impossible, because a
   hand-split that sums to e.g. £130 will rarely be all-round-number.

What we already have working in our favour:

- **Pot is always known**: `Price × ApprovedMembers` (and the read side already
  surfaces `TotalPrizePot = EntryCount × EntryCost`).
- **Pluggable payout strategies**: `IPrizeStrategy` (Round / Monthly / Overall /
  MostExactScores) with tie-pooling and `PrizeDistributionHelper`. We keep these
  as the *settlement* engine; we add a *projection* engine in front.
- **Money never moves through the app** (ADR-0003). This whole feature is about
  *display + configuration*, not payments. That keeps it low-risk legally.
- **Tournament sections already exist** in data: `TournamentRoundMappings.Stages`
  (e.g. `GroupStage|Knockout`) — enough to build a "Section" category on.

So the work is: **replace "manual, exact, post-deadline amounts" with a
declarative scheme that an evaluator turns into a live, round-number breakdown,
frozen automatically at the deadline.**

---

## The sub-problems any design must solve

1. **Split the pot across enabled categories** (Overall vs Round vs Section vs
   Exact Scores).
2. **Decide how many places pay** within a category, scaling with entrants.
3. **Keep every prize a round number** while still accounting for 100% of the pot
   (i.e. define where the remainder goes — this *replaces* the exact-penny rule).
4. **Compute the marginal effect** of one more entrant (`breakdown(N+1) − breakdown(N)`)
   for the "+£x in green" UX.

The three options below differ mainly in **how 1–3 are expressed and who
controls them.**

---

## Option A — Percentage scheme + product-defined payout curve

Admin toggles categories on/off and assigns each a **percentage of the pot**
(sliders constrained to total 100%). *How many places pay and the split between
them* comes from a **central, product-owned payout curve** keyed to entrant
count (not admin-editable). A single rounding pass snaps each prize to the
nearest "round step" (e.g. £5) and the leftover is added to the top prize.

Stored config: `{ category, percentage }` rows. Amounts are always derived.

**Pros**
- Simplest admin UX: a few toggles + sliders + boost checkboxes.
- One consistent, well-tested payout curve across all leagues.
- Live + dynamic for free; marginal "+£x" is just evaluate-at-N vs N+1.
- Hard to misconfigure into something silly.

**Cons**
- Percentages → round numbers is fiddly for small pots (10% of £30 = £3, fine;
  but 30% of £130 = £39 → snap to £40 → remainder juggling every join).
- Breakdown can "jump around" as members join (a place appears/disappears,
  amounts re-snap) — can feel unstable to watchers.
- Admins who want "1st = £100 flat" can't express that.
- The remainder-to-top-prize rule means the headline prize wobbles by the round
  step on most joins.

---

## Option B — Admin-authored rule tiers

Per enabled category, the admin authors **threshold rules**: payout tiers keyed
to entrant or pot bands. E.g. Overall: "≥20 entrants → pay 50/30/20 across top 3;
else 1st takes all". The evaluator picks the matching tier for the current pot.

**Pros**
- Maximum flexibility and control.
- Still live-computable and dynamic.

**Cons**
- Heavy: a rule-builder UI is a big build and a lot for a casual admin to use.
- Easy to misconfigure; needs extensive validation + previews.
- "Round numbers" must be enforced across arbitrary admin rules — the hardest
  place to guarantee it.
- Overkill for the vast majority of leagues. **Not recommended as the primary
  model**, though tier *thresholds* (how many places light up) are reusable as a
  product default in A or C.

---

## Option C — Unit-share pot (recommended)

**Express every prize as a whole number of "units", where one unit = one entry
fee.** Because the pot grows in exact increments of the entry fee, if each prize
is `k × entryFee`, then **every prize is automatically a round number** (whenever
the fee is round) and **the units always sum to exactly N** — the exact-penny
problem and the remainder problem both disappear by construction.

How it works:

- Pot = `E × N` (E = entry fee, N = approved entrants) = **N units**.
- Each category has a **ladder** expressed in units and a **priority order** for
  how incoming units "fill" prize slots. Example default for an Overall-only
  league with a `3-2-1` ladder:
  - 1st unit → 1st place (1 unit)
  - 2nd unit → 1st place (now 2 units = 2×E)
  - …more places switch on as more units arrive, following the ladder.
- **Round** and **Section** categories claim one (or k) units per round/section
  as those complete; the ladder tells the engine the priority between "fund the
  next Overall place" vs "fund a Round prize".
- As N grows, **more units exist → more places/categories light up** — winner
  count scaling is intrinsic, not a special case.

Why this fits the brief almost perfectly:

- **Round numbers**: guaranteed (every prize is `k × E`).
- **Winner count scales with entrants**: intrinsic (more units → more slots).
- **"+£x where your money goes"**: the new entrant's single unit lands on a
  *specific* next slot in priority order — we can literally highlight that slot
  green with "Your £10 funds the new 3rd-place prize" / "adds £10 to 1st".
- **Trivially testable** to 100% coverage (pure integer arithmetic).
- No remainder ambiguity, so the displayed pot is always fully and exactly
  allocated.

**Cons / things to settle**
- Prizes are constrained to **multiples of the entry fee**. A £10 league can't
  show "1st = £25". For a round-numbers-only product this is acceptable (it's
  arguably the *point*), but it is a real limitation.
- **Non-round entry fees** (£7.50) break the round-number guarantee → likely
  constrain entry fees to whole pounds (or a chosen step) **when prizes are on**.
- **Free leagues** (E = 0) have no pot → scheme becomes informational only
  (fine — free leagues have no prizes today anyway).
- Needs a sensible **default ladder + priority order** per category — a one-time
  product decision (and the natural place to reuse Option B's threshold idea:
  "don't light up 2nd place until ≥ X units").

### Recommendation

**Option C as the engine**, with the *threshold table* idea from B governing
when extra places light up, and an **optional `PrizeFundOverride`-style manual
escape hatch** (the column already exists) for the rare admin who wants bespoke
amounts post-deadline via the existing `DefinePrizeStructure` path. Option A is
the fallback if "prizes must be exact percentages / not tied to the fee" turns
out to matter more than "always clean round numbers".

### Option C, refined — "block apportionment"

The cleanest concrete form of C. The unit is **not** the whole entry fee but a
smaller **block** `B`, so one entry can feed several categories at once.

**Block size — decided: `B = £1`, fixed (not admin-chosen).** Reasons:
- Most blocks to play with → tracks the ideal % split closely and lets more
  places light up on small pots.
- `£1` divides *any* whole-pound stake → no per-league validation, no admin knob.
- Keeps the marginal "+£x" perfectly **stable**: with `£1` blocks the per-entry
  "chips" are literally *the pounds of the stake*, so each entrant adds the exact
  same whole-pound amount to each category on every join (no rounding drift).
- Still round — whole pounds, never pence.

Trade-off: amounts look like `£98/£59/£39`, not chunky `£100/£60/£40`. If the
£5-clean *look* matters, add it later as an optional **cosmetic** "round to
nearest £5 where the pot allows" pass — do **not** make block size an admin
setting (extra confusion, starves small leagues of places). Implication:
**stakes must be whole pounds when prizes are on.**

**Definitions**
- **Stake `E`** — whole pounds when prizes are on. `blocksPerEntry = E` (in £1s).
- **Per-entry category split**: the admin allocates the **pounds of each entry**
  across the enabled categories; the allocation must sum to `E`. e.g. £25 →
  `Overall £15 / Section £5 / Exact £5`. (Input is pound sliders / % snapped to
  whole pounds — *not* 25 literal draggable chips.) This is the headline "where
  does my money go" lever, and what we visualise to prospective members.

**Category registry (extensible — supports "I may add more prize types")**

Categories are a data-driven registry, each declaring a default weight, a
*kind*, and its split behaviour. Toggling a category recomputes the recommended
per-entry split by **renormalising the default weights across whatever is
enabled** (Overall-only → 100% overall; Overall+Exact → 75/25; etc.). Adding a
new type later = one registry row (+ a strategy if it scores differently) — no
engine rewrite. This rides on the existing pluggable `IPrizeStrategy`.

| Category | Default weight | Kind | Split behaviour |
|---|---|---|---|
| Overall | 3 | EndOfSeason | places-ladder (table below) |
| Section (groups/knockouts) | 2 | Staged | sub-pot 50/50 across the 2 stages, each stage uses the Overall ladder |
| Most Exact Scores | 1 | EndOfSeason | places-ladder (often just winner-takes-all at small N) |
| Round | 1 | Recurring | **not split** — winner-takes-all per round |
| Monthly | 1 | Recurring | **not split** — winner-takes-all per month |

**Evaluation (live, at any N)**
1. Category sub-pot (£) = `perEntryAllocation × N`. Always exact, always whole-£.
2. **EndOfSeason** categories: split the sub-pot across ranks using the
   **threshold table** for the current N (below), apportioned by
   **largest-remainder** (convert each rank's % to £, floor, hand leftover £ out
   one at a time by largest fractional remainder, **ties to the higher rank**).
   Every rank is whole pounds and they sum *exactly* to the sub-pot.
3. **Staged** (Section): there are always exactly 2 stages, so divide the section
   sub-pot **50/50** between Group stage and Knockouts (admin-adjustable), then
   rank within each stage using the **same Overall ladder**. Smaller stage pots →
   fewer places light up automatically.
4. **Recurring** (Round/Monthly): not split into places. Per-event prize =
   `floor(categorySubPot ÷ numberOfEvents)` in whole pounds, rising as entrants
   grow (e.g. £190 round pot ÷ 38 rounds = £5 to each weekly winner). Any small
   remainder rolls into the final event (or the overall top prize — to confirm).
   Ties within an event split that event's prize equally.
5. **Dynamic winner count** falls out two ways that reinforce: the table sets how
   many places a band *wants* to pay, and any place that apportions to **£0
   simply doesn't exist** — so tiny pots self-limit.

**Default threshold table** (admin-editable behind an "advanced" toggle;
validated: sums to 100, descending, no prize below £1):

| Entrants | Places | Split (%) |
|---|---|---|
| 2–5   | 1 | 100 |
| 6–10  | 2 | 70 / 30 |
| 11–20 | 3 | 50 / 30 / 20 |
| 21–40 | 4 | 50 / 25 / 15 / 10 |
| 41–75 | 5 | 40 / 25 / 15 / 12 / 8 |
| 76+   | 6 | 35 / 22 / 15 / 12 / 9 / 7 |

**Worked example** — `B = £1`, `E = £25`, split `Overall £15 / Section £5 /
Exact £5`, growing 12 → 13 entrants:

| Slot | N=12 | N=13 | Joiner adds |
|---|---|---|---|
| Overall sub-pot (£15/entry) | £180 | £195 | +£15 |
| → 1st (50%) | £90 | £98 | +£8 |
| → 2nd (30%) | £54 | £58 | +£4 |
| → 3rd (20%) | £36 | £39 | +£3 |
| Section (£5/entry) | £60 | £65 | +£5 |
| Exact scores (£5/entry) | £60 | £65 | +£5 |

→ prospective-member copy: *"Your £25 adds £15 to the overall prizes, £5 to the
section pot, and £5 to exact scores."* The **category** delta is always exactly
the per-entry allocation (rock-stable); the per-rank deltas come from diffing
`breakdown(N)` vs `breakdown(N+1)` and vary by a pound or two as £1 blocks
re-apportion (the headline category figure does not).

**Leftover-allocation choice** (one product call): largest-remainder (most
accurate to the %, but a leftover block can occasionally bump 2nd before 1st
between consecutive joins) vs **top-rank-first** (perfectly monotonic, "1st always
rounds up", tiny accuracy cost). Lean: largest-remainder, ties to higher rank.

(Section and Round/Monthly behaviours are defined in the category registry and
evaluation steps 3–4 above.)

---

## Orthogonal pieces (needed regardless of A/B/C)

### 1. Admin configuration on Create/Edit

- New **"Prizes & Boosts"** section on `Create.razor` / `Edit.razor`.
- **Category toggles**: Overall, Round, Section (groups/knockouts), Most Exact
  Scores. *Section* is only offered when the season has `TournamentRoundMappings`
  (a tournament, not a league season).
- **Boost selection**: surface the `BoostDefinitions` catalogue as checkboxes →
  write `LeagueBoostRules` (`IsEnabled`, `TotalUsesPerSeason`, optional
  `LeagueBoostWindows`). **There is no admin UI for boosts today — they're
  DB-only**, so this is net-new (new command + handler + validators + endpoint).
- Editability window (fairness): the scheme should follow the same lock pattern
  as scoring/price — freely editable until the **first member joins**, then
  locked (or additive-only). Showing prizes up front is effectively a promise to
  joiners, so we shouldn't let admins move the goalposts after people pay.

### 2. Replacing the deadline lock

- The **scheme** is set at create/edit time; the **concrete breakdown is derived
  live**. At the deadline, a job/handler **freezes** the scheme + final pot into
  the existing `LeaguePrizeSettings` rows (the settlement engine is unchanged
  downstream).
- Keep `DefinePrizeStructure` as a **site-admin manual override** for edge cases,
  but it's no longer the primary path.

### 3. A new "Section" prize category

- Add `Section` to the `PrizeType` enum + a `SectionPrizeStrategy : IPrizeStrategy`
  that groups rounds by `TournamentRoundMappings.Stages` (Group stage vs
  Knockouts) and awards best aggregate per section.
- Schema + `DatabaseTools` + `database-schema.md` updates per CLAUDE.md rules.

### 4. Surfacing to the three audiences

- **Prospective member**: a read-only **prize-preview** view (pot, scheme,
  live breakdown at current N, **+£E delta**). For private leagues this is keyed
  by the entry code they're about to use (no member PII leaked — just numbers).
- **Current member / admin**: "Projected prizes at N entrants" panel on the
  league page, with a "finalises at the deadline" note; after the deadline show
  the frozen structure (existing winnings views).
- **The "+£x in green"**: render `breakdown(N+1) − breakdown(N)`, with green
  deltas next to affected slots and copy like *"Your £10 adds £10 to the 2nd-place
  prize."*

## Acceptance Criteria

- [ ] Admin can enable/disable each prize category and select boosts from
      Create/Edit, before members join.
- [ ] A live, **round-number** breakdown is computable at any time from the
      current pot, with a defined home for any remainder.
- [ ] Number of paid places increases as entrants increase, per a clear rule.
- [ ] Prospective members see the current breakdown **and** the +£x effect of
      their own entry, clearly attributed.
- [ ] At the deadline the scheme freezes into `LeaguePrizeSettings`; settlement
      (Winnings/LeaguePayouts) is unchanged.
- [ ] Free leagues and non-round entry fees behave sensibly (defined, not crash).
- [ ] Domain stays at 100% line/branch coverage.

## Data Model Sketch (for Option C)

- `LeaguePrizeScheme` (or columns on `Leagues`): which categories are on +
  ladder/priority config. Likely a child table `LeaguePrizeSchemeEntries`
  (`Category`, `UnitWeight`/`MaxPlaces`/threshold config).
- `LeaguePrizeSettings` / `Winnings` / `LeaguePayouts`: **unchanged** — they
  remain the frozen, post-deadline settlement artefacts the scheme generates.
- Boosts: new write path into existing `LeagueBoostRules` / `LeagueBoostWindows`.
- Add `Section` to `PrizeType`.

## Decision / ADR implications

This is a cross-cutting product + financial decision → warrants an ADR once a
direction is chosen (likely **0011**). It interacts with:

- **ADR-0003 (no custody of prize money)** — *unaffected*: still display/tracking
  only, no money moves.
- **ADR-0010 (peer-to-peer settlement)** — the frozen `LeaguePrizeSettings` →
  `Winnings` → `LeaguePayouts` flow is preserved; we only change how the *initial*
  settings are produced.

## Open Questions

Settled (confirm):
- **Block size = £1, fixed.** Stakes must be whole pounds when prizes are on.
  Prizes are whole pounds; remainder problem designed out.
- **Toggles drive the recommendation**; default category weights renormalise
  across whatever is enabled; categories are an extensible registry.
- **Round / Monthly**: not split — winner-takes-all per event, per-event prize
  = `floor(subPot ÷ events)`.
- **Section**: split 50/50 across the 2 stages by default, each stage using the
  Overall places-ladder.
- "How many places pay" = the **default threshold table** above, admin-editable.

Still open:
- [ ] Leftover-£ rule: **largest-remainder (ties to higher rank)** vs
      top-rank-first/monotonic? (Lean: largest-remainder.)
- [ ] Where does the recurring **rounding remainder** go — final event, or roll
      to the overall top prize?
- [ ] Per-entry split input — pound sliders, % snapped to whole £, or named
      presets ("Mostly overall" / "Balanced")? And do admins get the "advanced"
      rank-table editor at launch, or just the defaults?
- [ ] Optional cosmetic **"round to nearest £5"** pass — launch or later?
- [ ] Can the admin still hand-tune amounts after the deadline (keep
      `DefinePrizeStructure` as an override), or is the scheme the only path?
- [ ] How locked is the scheme once members join — fully locked, or additive-only?
- [ ] For prospective members of **private** leagues, is entry-code-keyed preview
      the right gate, or do we want a richer pre-join landing page?
