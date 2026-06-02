# Feature: Live Prize Pot & Configurable Prize Scheme

## Status

**Implemented** on branch `dynamic-prize-pot` (Option C - block apportionment, per ADR-0011).

> Originally a thinking document. The chosen direction (Option C) is now built:
> domain apportionment engine, evaluation/registry, persistence, create/edit
> commands with write-once, boost admin, the create/edit UI, prospective
> surfacing with the +£x delta, and the Section prize + lazy deadline freeze.
> Migration SQL is applied out of band (see the task notes / chat).

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

**Block size — decided: `B = £1`, fixed (not admin-chosen).** Granularity and
divisibility win: a £10 stake is **10 blocks**, so it can fund several categories
at once; £5 blocks would give only 2 and couldn't support 3 prizes. Stakes are
whole pounds (no pence) so blocks stay whole.

**£5 rounding — Overall prizes only, above a threshold.** Grounded in real use:
last season (17 players) the prizes were `38 × £4` round, `£48` exact, `£25`/month,
and `£220/£120/£90` overall — i.e. only the **headline overall** prizes were
£5-clean; everything else kept its natural £1 value. So:
- **Overall**: if the overall sub-pot `S ≥ £x` (suggest **£100**), **floor `S` to
  the nearest £5** so *every* overall rank (1st included) is a clean £5, then
  apportion that £5-multiple across ranks via the table (top-down leftover). The
  odd **£1–£4** (`S mod 5`) **spills into another category's fund** (see below).
  Below `£x`, stay at £1 (don't distort small pots).
- **Round / Monthly / Exact / Section**: keep natural **£1** values (the £4, £25,
  £48 above) — no £5 rounding; these also **receive** the overall spillover.

> Supersedes the earlier `B = £5` proposal. £5 blocks guaranteed every prize was
> £5-clean and summed exactly, but were too coarse to fund multiple categories
> from small stakes. £1 blocks + overall-only £5 rounding match how prizes are
> actually set (only the headline figures are "pretty").

**Remainder rule — spill the odd £1–£4 out of Overall into another fund.** Rather
than dumping the remainder on 1st (which left it looking like £222), the
floor-to-£5 leftover moves into a £1-granular category, which absorbs it naturally:
- **Conserved & stateless**: the breakdown is a *pure recompute* from
  `(scheme, pot, N)` — the spillover is **never stored**. As entrants change during
  registration the odd pounds move freely between funds and **can come back**;
  only the deadline freeze is permanent. The sum across *all* categories always
  equals the pot exactly — spillover only changes *which bucket* holds the odd
  £1–£4, never creating or losing money.
- **Target priority** (deterministic): Round → Most Exact Scores → Monthly →
  Section. **Fallback**: if **Overall is the only category**, there is nowhere to
  spill, so 1st absorbs the remainder (the old rule).
- **Expected to oscillate** ("on and off") as `N` changes — biggest when the
  per-entry overall allocation isn't a multiple of £5; ≈0 when it is. Always
  truthful and always sums to the pot.
- Within Overall, leftover £5 blocks are still handed out **top-down** (1st first)
  so growth is monotonic and the top prize never rounds down.

**Definitions**
- **Stake `E`** — whole pounds when prizes are on. `blocksPerEntry = E` (in £1s).
- **Per-entry category split**: the admin allocates the **pounds of each entry**
  across the enabled categories; the allocation must sum to `E`. e.g. £10 stake →
  10 chips spread across Overall / Round / Exact / (Section|Monthly). This is the
  "where does my money go" lever, and what we visualise to prospective members.
- **Admin thinks in target prizes, engine works in allocations** — so the editor
  shows the **derived prize amounts live** as the admin moves the sliders ("at 17
  entrants: £4/round, £48 exact, £220/£120/£90 overall, £25/month"). They tune
  allocations until the resulting prizes look right; the amounts freeze at the
  deadline (which is before kick-off, so the pot is final before any prize pays).

**Category registry (extensible — supports "I may add more prize types")**

Categories are a data-driven registry, each declaring a default weight, a
*kind*, an *availability gate*, and its split behaviour. Toggling a category
recomputes the recommended per-entry split by **renormalising the default weights
across whatever is enabled** (Overall-only → 100% overall; Overall+Exact → 75/25;
etc.). Adding a new type later = one registry row (+ a strategy if it scores
differently) — no engine rewrite. Rides on the existing pluggable `IPrizeStrategy`.

| Category | Weight | Kind | Available for | Split behaviour |
|---|---|---|---|---|
| Overall | 3 | EndOfSeason | all | places-ladder + floor-to-£5 (above £x); odd £1–£4 spills out |
| Section (groups/knockouts) | 2 | Staged | **tournaments only** | sub-pot 50/50 across the 2 stages, each uses the Overall ladder; £1 |
| Most Exact Scores | 1 | EndOfSeason | all | single prize (or top few); £1 |
| Round | 1 | Recurring | all | **not split** — winner-takes-all per round; £1 |
| Monthly | 1 | Recurring | **seasons only** (tournaments too short) | **not split** — per month; £1 |

**Evaluation (live, at any N)**
1. Category sub-pot (£) = `perEntryAllocation × N`. Always exact, whole pounds.
2. **Overall**: below `£x`, split `S` at £1 granularity by the **threshold table**.
   Above `£x`, floor `S` to the nearest £5, apportion that £5-multiple across ranks
   (all ranks clean £5, top-down leftover), and **spill the `S mod 5` remainder**
   into the next category by priority (Round → Exact → Monthly → Section; fallback
   1st if Overall is the only category).
3. **Staged** (Section): exactly 2 stages → divide the section sub-pot **50/50**
   (admin-adjustable), then rank within each stage using the **same ladder**.
   Smaller stage pots → fewer places light up automatically. £1 granularity.
4. **Recurring** (Round/Monthly): not split into places. Per-event prize =
   `floor(categorySubPot ÷ numberOfEvents)` in whole pounds (e.g. round sub-pot
   ÷ 38 rounds). Small remainder rolls into the final event (or 1st overall — to
   confirm). Ties within an event split that event's prize equally.
5. **Most Exact Scores**: single prize at £1 granularity (optionally top few).
6. **Dynamic winner count** falls out two ways that reinforce: the table sets how
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

**Worked example** — `B = £1`, `E = £13`, split `Overall £8 / Round £3 / Exact £2`
(non-£5 overall allocation, so spillover oscillates), 12 → 13 entrants
(Overall 50/30/20, rounding on — illustrative threshold £50 — spillover → Round):

| Slot | N=12 | N=13 | Joiner adds |
|---|---|---|---|
| Overall raw `S` | £96 | £104 | — |
| → floored to £5 | £95 | £100 | — |
| → 1st (50%) | £50 | £50 | +£0 |
| → 2nd (30%) | £30 | £30 | +£0 |
| → 3rd (20%) | £15 | £20 | +£5 |
| **Spill → Round fund** | **+£1** (96 mod 5) | **+£4** (104 mod 5) | +£3 |
| Round fund (£3/entry + spill) | £37 | £43 | +£6 |
| Exact fund (£2/entry) | £24 | £26 | +£2 |

Every overall prize is a clean £5; the odd £1/£4 lands in the Round fund (which is
£1-granular, so it just nudges the per-round prize). The **total** across all funds
is exactly the pot (£156, £169). The spill "comes and goes" with `N` — nothing is
stored, so it can move back. The pot-level delta a joiner adds is always exactly
their stake (£13), just distributed across buckets.

→ prospective-member copy: *"Your £13 adds £8 to the overall prizes and £5 to the
weekly round prizes."* The **pot-level** delta is always exactly the stake (money
is conserved); per-category and per-rank deltas come from diffing `breakdown(N)`
vs `breakdown(N+1)` and may shift as the £5 spillover comes and goes — always
truthful, always summing to the stake.

(Block size £1, overall-only £5 rounding, leftover rule top-down, category gating
and the Section / Round / Monthly
behaviours are all decided above — see the block-size note and evaluation steps.)

---

## Orthogonal pieces (needed regardless of A/B/C)

### 1. Admin configuration on Create/Edit

- New **"Prizes & Boosts"** section on `Create.razor` / `Edit.razor`.
- **Category toggles**: Overall, Round, Most Exact Scores (all competitions);
  **Section** only for **tournaments** (have `TournamentRoundMappings` stages);
  **Monthly** only for **seasons** (tournaments are too short). Gate the toggles
  by competition type.
- **Live derived-prize preview**: as the admin moves the per-entry allocation
  sliders, show the resulting prize amounts at the current entrant count
  (round/exact/overall/monthly), so they tune to a shape they like.
- **Boost selection**: surface the `BoostDefinitions` catalogue as checkboxes →
  write `LeagueBoostRules` (`IsEnabled`, `TotalUsesPerSeason`, optional
  `LeagueBoostWindows`). **There is no admin UI for boosts today — they're
  DB-only**, so this is net-new (new command + handler + validators + endpoint).
- **Editability — write-once.** The prize/boost section is editable **only while
  the scheme is unset**:
  - **New leagues**: set at creation → **locked thereafter** (cannot be changed).
  - **Existing schemeless leagues** (the in-flight World Cup): can be set **once**
    via Edit, then locked — this is the migration path for live leagues.
  - Once set, the Edit page renders the section read-only.
  - Open: do we want a **site-admin escape hatch** to correct a mistaken scheme?


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

- [ ] Admin sets categories + per-entry allocation + boosts from Create (new
      leagues) or once via Edit (existing schemeless leagues); locked once set.
- [ ] Category toggles gated by competition type (Section→tournaments,
      Monthly→seasons).
- [ ] A live breakdown is computable at any time from the current pot; every
      prize is a round number and the breakdown sums exactly to the pot.
- [ ] Overall prizes round to £5 above the threshold (1st absorbs the odd £1–£4);
      Round/Monthly/Exact/Section stay at £1.
- [ ] Number of paid places increases as entrants increase, per the table.
- [ ] Prospective members see the current breakdown **and** the +£x effect of
      their own entry, clearly attributed.
- [ ] At the deadline the scheme freezes into `LeaguePrizeSettings`; settlement
      (Winnings/LeaguePayouts) is unchanged.
- [ ] Free leagues behave sensibly (scheme informational only; defined, not crash).
- [ ] Domain stays at 100% line/branch coverage.

## Data Model Sketch (for Option C)

- `LeaguePrizeScheme` (or columns on `Leagues`): which categories are on, the
  per-entry pound allocation per category, the £5-rounding threshold, and a
  **`SetAtUtc`/`IsLocked`** marker enforcing write-once. Likely a child table
  `LeaguePrizeSchemeEntries` (`Category`, `PerEntryPounds`, optional rank-table
  override).
- `LeaguePrizeSettings` / `Winnings` / `LeaguePayouts`: **unchanged** — they
  remain the frozen, post-deadline settlement artefacts the scheme generates.
- Boosts: new write path into existing `LeagueBoostRules` / `LeagueBoostWindows`.
- Add `Section` to `PrizeType`; `Monthly` already exists.
- Need a reliable **season-vs-tournament** signal to gate Section/Monthly
  (presence of `TournamentRoundMappings`, or a `Competitions` type flag — confirm).

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
- **Block size = £1, fixed.** Stakes are whole pounds. £1 granularity so a small
  stake can still fund several categories.
- **£5 rounding on Overall only, above a threshold `£x` (suggest £100)**: floor `S`
  to £5 so every overall prize is clean, and **spill the odd £1–£4 into another
  category** (priority Round → Exact → Monthly → Section; fallback 1st if Overall
  is the only category). Round/Monthly/Exact/Section stay £1.
- **Spillover is conserved & stateless** — recomputed each `N`, never stored, so it
  moves freely and can come back; the total across all funds always equals the
  pot. Within Overall, leftover £5 blocks go top-down (monotonic, 1st never rounds
  down).
- **Toggles drive the recommendation**; default category weights renormalise
  across whatever is enabled; categories are an extensible registry.
- **Category gating**: Section → tournaments only; Monthly → seasons only.
- **Round / Monthly**: not split — winner-takes-all per event, `floor(subPot ÷ events)`.
- **Section**: split 50/50 across the 2 stages by default, each using the ladder.
- **Write-once scheme**: set at creation (locked) for new leagues; settable once
  via Edit for existing schemeless leagues (World Cup), then locked. **A site
  admin can override a locked scheme** to fix mistakes (league admins cannot).
- **Allocation input = pound sliders + live derived-prize preview** (one slider
  per enabled category, must total the stake).
- "How many places pay" = the **default threshold table** above, admin-editable.

Still open (minor — defaults proposed):
- [ ] Confirm the **£5-rounding threshold** value (£100 overall sub-pot?).
- [ ] Confirm the **spillover target priority** (Round → Exact → Monthly → Section?).
- [ ] Where does the recurring **rounding remainder** go — final event, or 1st overall?
- [ ] Does the **"advanced" rank-table editor** ship at launch, or just defaults?
- [ ] What season-vs-tournament signal gates Section/Monthly (TournamentRoundMappings
      presence vs a Competitions type flag)?
- [ ] For prospective members of **private** leagues, is entry-code-keyed preview
      the right gate, or do we want a richer pre-join landing page?

## Implementation Plan

See ADR [0011](../../../decisions/0011-dynamic-prize-pot.md) for the decision
record. Tasks (to be built in order; each is a separate file):

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Domain: prize scheme & apportionment](./01-domain-scheme-and-apportionment.md) | `PrizeScheme` entity (write-once), pure apportionment + £5-rounding service, 100% coverage | Done |
| 2 | [Evaluation engine & category registry](./02-evaluation-engine-and-registry.md) | Live breakdown from (scheme, pot, N); registry with kinds + gating; threshold table | Done |
| 3 | [Persistence & schema](./03-persistence-and-schema.md) | `LeaguePrizeScheme(+Entries)` tables, repo write path, DatabaseTools + schema doc | Done |
| 4 | [Create/Edit commands & write-once](./04-create-edit-commands.md) | Set-scheme command (write-once + site-admin override), validators, replace deadline lock | Done |
| 5 | [Boost admin configuration](./05-boost-admin-config.md) | Catalogue query + command to write `LeagueBoostRules`/`Windows` (net-new admin path) | Done |
| 6 | [Create/Edit UI](./06-create-edit-ui.md) | "Prizes & Boosts" section: toggles, pound sliders, live preview, boost checkboxes, locked state | Done (rank-table advanced editor: defaults only - see note) |
| 7 | [Prospective & member surfacing](./07-surfacing-and-delta.md) | Prize-preview query/endpoint, +£x delta, projected-prizes panel | Done |
| 8 | [Section prize + freeze-at-deadline](./08-section-and-freeze.md) | `Section` PrizeType + strategy; freeze scheme → `LeaguePrizeSettings` at deadline | Done |

> **Decisions taken during build** (from the owner): recurring-remainder spills to
> Most Exact Scores (else Overall); the prospective preview is a dedicated screen
> reached via the entry code, including admin top-up money. The freeze is **lazy**
> (runs at the first round-processing after the deadline) - no scheduler needed.
>
> **Remaining UI polish (follow-up):** the per-league *advanced rank-table editor*
> ships as defaults-only in the UI for now (the storage column `RankTableJson` and
> the whole evaluation pipeline already support per-league overrides; only the
> editor widget is outstanding). Wiring the prospective-preview link into the
> existing join-by-code entry box is also a small follow-up.
