# Displaying Knockout Results (Extra Time & Penalties)

## Status

Not Started | **In Progress (plan only)** | Complete

> This document is a **complete, self-contained implementation plan**. Another
> session should be able to execute it end-to-end without asking further
> questions. Read [`tournament-knockout-scoring`](../tournament-knockout-scoring/README.md)
> first — this feature is the **display** follow-on to that **scoring** feature.

---

## 1. Goal & background

Tournament knockout matches can finish after extra time (AET) or a penalty
shootout (PEN). The **prediction is already scored on the 90‑minute result**
and that part is correct and must not change (see §2). What is missing is the
**display**: today we throw the AET/penalty data away, so a knockout decided on
penalties renders as e.g. `1 - 1` with a green tick — indistinguishable from a
draw, which is impossible in a knockout and looks wrong.

We will:

1. **Capture** the after‑extra‑time aggregate and the penalty‑shootout score
   when a knockout match finishes past 90 minutes (currently discarded).
2. **Keep the 90‑minute score as the primary, scored result** everywhere.
3. **Add a secondary, less‑prominent caption** under the result reading
   `4-2 pens` / `2-1 (a.e.t.)`, so users can see we know the real outcome but
   that the **game** uses the 90‑minute score.
4. **Explain the rule once per round** with a small legend, so it is obvious
   *why* without cluttering every cell.

### Wording (decided)

| Situation | Caption |
|-----------|---------|
| Decided in extra time | `2-1 (a.e.t.)` |
| Decided on penalties | `4-2 pens` |
| Decided in 90 minutes | *(no caption — unchanged)* |
| Extra time / shootout **in progress** | *(no caption; 90′ score shown, pulsing — see §4.5)* |

No `Final:` prefix — the primary badge above already establishes "this is the
result", and `(a.e.t.)` / `pens` carry the meaning on their own. This also
shortens the caption enough to fit one line in the narrow desktop column. The
fuller "Final result … scored on the 90-minute result" sentence still lives in
the hover tooltip (§4.2).

**Penalties show only the shootout score** (`4-2 pens`), not the deadlocked
score. The decisive information is the shootout; the level score is already
visible in the primary 90′ badge above. In the rare case where goals are scored
*during* extra time (e.g. 1‑1 at 90′, 2‑2 after ET, then pens), the after‑ET
aggregate is not shown on the line but **is retained in the tooltip** — so no
information is lost. For an **extra‑time** finish the caption keeps the score
(`2-1 (a.e.t.)`) because there the score is the meaningful outcome. The
after‑ET score (`2-1`) is API‑Football `goals`, not the 90‑minute score.

---

## 2. Current behaviour — confirmed correct, DO NOT change

* `UpdateScoresForNextRoundCommandHandler.GetScoreForMatch()`
  (`src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateScoresForNextRoundCommandHandler.cs:69`)
  already uses `score.fulltime` (90‑minute) for knockout matches and falls back
  to `goals` otherwise.
* That 90‑minute score is stored in `Match.ActualHomeTeamScore` /
  `ActualAwayTeamScore` and is the **only** thing `UserPrediction.DetermineOutcome()`
  compares against. **Scoring stays on 90 minutes.**
* The only score‑writing path is `UpdateScoresForNextRoundCommandHandler`
  (the `UpdateAllLiveScoresCommandHandler` just fans out to it per active
  season, and the admin "Enter Results" screen routes through the same
  `UpdateMatchResultsCommandHandler`). There is **one** capture point to change.

---

## 3. Where scores are displayed — full surface audit

Every place a score is rendered, and whether the AET/penalty caption applies.
**Only the *actual match result* surfaces are in scope** — surfaces that render
a *user prediction* never show AET.

| # | Surface | File | Renders | AET caption? | Action |
|---|---------|------|---------|--------------|--------|
| 1 | Desktop results grid — **actual result** | `PredictionGrid.razor:46‑58` → `MatchStatusBadge` | actual 90′ score | **YES** | caption via `MatchStatusBadge` |
| 2 | Mobile match card — **actual result** | `MobileMatchResultCard.razor:40‑44` → `MatchStatusBadge` | actual 90′ score | **YES** | caption via `MatchStatusBadge` |
| 3 | Desktop grid — player prediction | `PredictionGrid.razor:87‑104` → `PredictionStatusBadge` / plain | a user's prediction | no | none |
| 4 | Mobile card — player prediction | `MobileMatchResultCard.razor:72‑79` → `PredictionStatusBadge` | a user's prediction | no | none |
| 5 | Prediction status badge | `PredictionStatusBadge.razor` | a user's prediction | no | none |
| 6 | Dashboard active‑round preview | `RoundCard.razor:45‑47` (`match-preview-score`) | the user's **predicted** score (no actual shown) | no | none |
| 7 | Prediction entry steppers | `Predictions.razor:117‑137` | prediction input | no | none |
| 8 | Admin "Enter Results" steppers | `EnterResults.razor:53‑75` | actual 90′ input | partial | **preserve** ET/pens on round‑trip (no visible change in phase 1) |
| 9 | Round points leaderboard | `RoundResultsTile.razor:82‑103` | points totals | no | none |
| 10 | Per‑round legend (new) | `RoundResultsTile.razor` | explanatory note | n/a | **add** (see §7) |

**Net result:** the only visible change is in **`MatchStatusBadge`** (one
component, rendered in two layouts — surfaces 1 & 2) plus a **new legend** in
`RoundResultsTile` (surface 10). Everything else is plumbing or untouched.

---

## 4. Display design (the part that needed deciding)

### 4.1 Principles

* **Primary = 90′ score, unchanged.** Same bold blue/green/yellow/red badge as
  today. The caption must read as clearly secondary.
* **Caption is muted and smaller**: lighter weight, smaller font, secondary
  colour. Never competes with the score.
* **Same wording on every surface** (`2-1 (a.e.t.)` / `4-2 pens`) — we do
  *not* invent per‑surface abbreviations. We make it fit instead (§4.3).
* **Explain once per round**, not per cell (§7).

### 4.2 `MatchStatusBadge` layout

Wrap the existing badge in a vertical stack and append the caption only when
present. The badge markup itself is unchanged.

```razor
<div class="match-result @(ShowFinalResult ? "match-result--with-detail" : "")">
    <div class="@BadgeClass">
        <span class="badge-icon"><i class="@IconClass"></i></span>
        @if (Status == MatchStatus.Scheduled)
        {
            <LocalTime UtcDate="@MatchDateTimeUtc" Format="dd/MM/yyyy 'at' HH:mm" />
        }
        else
        {
            <span>@ScoreText</span>
        }
    </div>

    @if (ShowFinalResult)
    {
        <span class="match-result-detail" title="@DetailTooltip">@DetailText</span>
    }
</div>
```

New parameters and computed members (UK spelling, statements on their own line):

```csharp
[Parameter] public int? AfterExtraTimeHomeScore { get; set; }
[Parameter] public int? AfterExtraTimeAwayScore { get; set; }
[Parameter] public int? PenaltyHomeScore { get; set; }
[Parameter] public int? PenaltyAwayScore { get; set; }

private bool WentToPenalties => PenaltyHomeScore.HasValue && PenaltyAwayScore.HasValue;

// Only completed knockout matches that needed extra time carry these values.
private bool ShowFinalResult =>
    Status == MatchStatus.Completed
    && AfterExtraTimeHomeScore.HasValue
    && AfterExtraTimeAwayScore.HasValue;

private string DetailText => WentToPenalties
    ? $"{PenaltyHomeScore}-{PenaltyAwayScore} pens"
    : $"{AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore} (a.e.t.)";

private string DetailTooltip => WentToPenalties
    ? $"Final result {AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore}, {PenaltyHomeScore}-{PenaltyAwayScore} on penalties. Predictions are scored on the 90-minute result."
    : $"Final result {AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore} after extra time. Predictions are scored on the 90-minute result.";
```

### 4.3 Thin columns (the key constraint) & light/dark CSS

The desktop grid column (`.results-grid .match-col`) is only `min-width: 6.5rem`
(~104px) and the cells are `white-space: nowrap`. The trimmed captions
(`4-2 pens`, `2-1 (a.e.t.)`) comfortably fit one line, but to be safe we let
**the caption (and only the caption) wrap** within the column rather than
overflow, while the badge stays on one line. The mobile card header is
full‑width, so the text always sits on one line there. The full sentence (with
the "scored on the 90‑minute result" explanation) is always available via the
`title` tooltip.

Add to **`src/ThePredictions.Web.Client/wwwroot/css/components/badges.css`**
(this file already owns `.badge-group`; co‑locate the `.theme-dark` override
here as `results-grid.css` does):

```css
.match-result {
    display: inline-flex;
    flex-direction: column;
    align-items: center;
    gap: 0.15rem;
}

/* Secondary, deliberately quieter than the score badge:
   smaller, lighter, muted colour. Allowed to wrap inside the
   narrow desktop results-grid column (cells are nowrap). */
.match-result-detail {
    max-width: 6rem;
    white-space: normal;
    line-height: 1.1;
    text-align: center;
    font-size: 0.6rem;
    font-weight: 600;
    letter-spacing: 0.02em;
    color: var(--grey-500);
    font-variant-numeric: tabular-nums;
}

.theme-dark .match-result-detail {
    color: var(--white-alpha-50);
}

/* Mobile card header has room — keep it on one line and a touch larger. */
@media (min-width: 768px) {
    .match-result-detail {
        font-size: 0.65rem;
    }
}
```

* **Light mode:** `var(--grey-500)` on the white grid / white card — clearly
  subordinate to the bold score, still legible. Matches the muted treatment
  already used by `.match-col-tbc-label` and `.match-preview-vs`.
* **Dark mode:** `var(--white-alpha-50)` — same token family used by the
  existing dark overrides in `results-grid.css`, so contrast is consistent.
* No new CSS *file* is created, so **no** `app.css` `@import` or
  `<CssFilesToBundle>` changes are required.

> Verify in both themes per `src/ThePredictions.Web.Client/CLAUDE.md` ("Always
> Verify Both Light and Dark Mode"), and on a real World Cup‑sized grid (24
> group matches) that the wrapped caption does not distort row heights badly.

### 4.4 Wire the caption into the two call sites

`PredictionGrid.razor` (~line 49) and `MobileMatchResultCard.razor` (~line 40)
both already pass `Status` / `HomeScore` / `AwayScore` to `MatchStatusBadge`.
Add four pass‑throughs to each:

```razor
<MatchStatusBadge Status="match.Status"
                  MatchDateTimeUtc="match.MatchDateTimeUtc"
                  HomeScore="match.ActualHomeTeamScore"
                  AwayScore="match.ActualAwayTeamScore"
                  AfterExtraTimeHomeScore="match.AfterExtraTimeHomeScore"
                  AfterExtraTimeAwayScore="match.AfterExtraTimeAwayScore"
                  PenaltyHomeScore="match.PenaltyHomeScore"
                  PenaltyAwayScore="match.PenaltyAwayScore" />
```

(`MobileMatchResultCard` uses `Match.` with a capital `M`.)

### 4.5 While extra time / penalties are in progress

**Primary stays the 90‑minute score, pulsing, throughout** — it is the scored
result, it will not change, and freezing it avoids the score visibly "jumping
back" to 90′ at the final whistle. The result caption (`4-2 pens` /
`2-1 (a.e.t.)`) appears **only when the match is final** (`ShowFinalResult`
requires `Status == Completed`), so nothing new is needed for the in‑progress
case in `MatchStatusBadge`.

**However, fix a latent status‑mapping gap (§6.3a) so the score does not vanish
mid‑match.** `GetMatchStatus` currently maps only `1H/HT/2H/ET` to
`InProgress`; the break before extra time (`BT`) and the penalty shootout (`P`),
plus generic `LIVE`, fall through to the default `Scheduled`. Because
`UpdateScore` clears the score on `Scheduled`, a knockout would briefly show its
kickoff time with **no score** during the break and the shootout. Map `BT`,
`P` and `LIVE` to `InProgress` so the 90′ score stays visible and pulsing right
through to `AET`/`PEN`.

**Optional (not in core):** a live "Extra time" / "Penalties" label during those
phases. The client only receives `MatchStatus.InProgress` today and cannot tell
extra time from a normal second half, so this needs a small phase signal plumbed
through `MatchInRoundDto`. Deferred to keep the core simple; add later if wanted.

---

## 5. Data model

### 5.1 New columns on `Matches` (all nullable, additive)

| Column | Type | Null | Meaning |
|--------|------|------|---------|
| `AfterExtraTimeHomeScore` | `int` | YES | Home score after extra time (API `goals.home`). Set only for knockout matches that went past 90′. |
| `AfterExtraTimeAwayScore` | `int` | YES | Away score after extra time (API `goals.away`). |
| `PenaltyHomeScore` | `int` | YES | Home penalty‑shootout score (API `score.penalty.home`). Set only when decided on penalties. |
| `PenaltyAwayScore` | `int` | YES | Away penalty‑shootout score (API `score.penalty.away`). |

`ActualHomeTeamScore` / `ActualAwayTeamScore` continue to hold the **90‑minute**
score for knockouts and are unchanged. Group‑stage and league matches leave all
four new columns `NULL`.

**Display is derived from these columns** (no enum change needed; the `AET`/`PEN`
distinction is recoverable):

* went to extra time ⇔ `AfterExtraTimeHomeScore IS NOT NULL`
* went to penalties ⇔ `PenaltyHomeScore IS NOT NULL`

### 5.2 Migration SQL (present in chat for the user — do NOT commit a `.sql` file)

**Additive** — safe to apply ahead of the code deploy.

```sql
ALTER TABLE [Matches] ADD
    [AfterExtraTimeHomeScore] INT NULL,
    [AfterExtraTimeAwayScore] INT NULL,
    [PenaltyHomeScore] INT NULL,
    [PenaltyAwayScore] INT NULL;
```

### 5.3 Docs & tooling

* **Update `docs/guides/database-schema.md`** — add the four columns to the
  `Matches` table section (around the current lines 271‑297).
* **`tools/ThePredictions.DatabaseTools` — no code change needed.**
  `DatabaseRefresher.cs` copies each table with `SELECT *` and intersects
  source/target columns dynamically (`DatabaseRefresher.cs:122,286`), so the new
  columns are copied automatically. `Matches` is already in `TableCopyOrder`.
  The columns are not personal data, so `DataAnonymiser` and
  `PersonalDataVerifier` need no changes. *(State this explicitly in the PR so a
  reviewer knows it was considered.)*

---

## 6. Capture logic (Application + Domain)

### 6.1 `Match` domain model — `src/ThePredictions.Domain/Models/Match.cs`

1. Add four `private set` properties:

```csharp
public int? AfterExtraTimeHomeScore { get; private set; }
public int? AfterExtraTimeAwayScore { get; private set; }
public int? PenaltyHomeScore { get; private set; }
public int? PenaltyAwayScore { get; private set; }
```

2. Add the four to the **public (Dapper) constructor** (line ~30) — append after
   `apiRoundName`, assign in the body.

3. Extend `UpdateScore` to carry them. Clearing rules: when a match is reverted
   to `Scheduled`/`Postponed`, clear the ET/pen columns too.

```csharp
public void UpdateScore(int homeScore, int awayScore, MatchStatus status,
    int? afterExtraTimeHomeScore, int? afterExtraTimeAwayScore,
    int? penaltyHomeScore, int? penaltyAwayScore)
{
    Guard.Against.Negative(homeScore);
    Guard.Against.Negative(awayScore);

    if (afterExtraTimeHomeScore.HasValue)
        Guard.Against.Negative(afterExtraTimeHomeScore.Value);

    if (afterExtraTimeAwayScore.HasValue)
        Guard.Against.Negative(afterExtraTimeAwayScore.Value);

    if (penaltyHomeScore.HasValue)
        Guard.Against.Negative(penaltyHomeScore.Value);

    if (penaltyAwayScore.HasValue)
        Guard.Against.Negative(penaltyAwayScore.Value);

    if (status is MatchStatus.Scheduled or MatchStatus.Postponed)
    {
        ActualHomeTeamScore = null;
        ActualAwayTeamScore = null;
        AfterExtraTimeHomeScore = null;
        AfterExtraTimeAwayScore = null;
        PenaltyHomeScore = null;
        PenaltyAwayScore = null;
    }
    else
    {
        ActualHomeTeamScore = homeScore;
        ActualAwayTeamScore = awayScore;
        AfterExtraTimeHomeScore = afterExtraTimeHomeScore;
        AfterExtraTimeAwayScore = afterExtraTimeAwayScore;
        PenaltyHomeScore = penaltyHomeScore;
        PenaltyAwayScore = penaltyAwayScore;
    }

    Status = status;
}
```

4. `Postpone()` (line ~139) — also null the four new columns.

> **Coverage:** Domain must stay at 100% line + branch. The four `HasValue`
> guards and both `if/else` arms need tests (see §8). Run
> `tools\Test Coverage\coverage-unit.bat`.

### 6.2 `MatchResultDto` — `src/ThePredictions.Contracts/Admin/Matches/MatchResultDto.cs`

```csharp
public record MatchResultDto(
    int MatchId,
    int HomeScore,
    int AwayScore,
    MatchStatus Status,
    int? AfterExtraTimeHomeScore = null,
    int? AfterExtraTimeAwayScore = null,
    int? PenaltyHomeScore = null,
    int? PenaltyAwayScore = null);
```

Defaults keep existing call sites compiling; the sync path and the admin path
both populate them (admin round‑trips existing values — §6.5).

### 6.3 `GetScoreForMatch` — `UpdateScoresForNextRoundCommandHandler.cs`

Return the ET/penalty figures too, driven by the API status. The 90′ logic is
unchanged.

```csharp
internal static (int HomeScore, int AwayScore,
                 int? AfterExtraTimeHomeScore, int? AfterExtraTimeAwayScore,
                 int? PenaltyHomeScore, int? PenaltyAwayScore)
    GetScoreForMatch(FixtureResponse fixture, Match localMatch, bool isTournament, string apiStatus)
{
    var ninetyMinuteHome = fixture.Goals!.Home.GetValueOrDefault();
    var ninetyMinuteAway = fixture.Goals.Away.GetValueOrDefault();

    if (isTournament && IsKnockoutMatch(localMatch))
    {
        var fulltime = fixture.Score?.FullTime;
        if (fulltime?.Home != null && fulltime.Away != null)
        {
            ninetyMinuteHome = fulltime.Home.Value;
            ninetyMinuteAway = fulltime.Away.Value;
        }

        // Only completed matches that actually went past 90 minutes.
        if (apiStatus is "AET" or "PEN")
        {
            var afterEtHome = fixture.Goals.Home.GetValueOrDefault();
            var afterEtAway = fixture.Goals.Away.GetValueOrDefault();

            int? penHome = null;
            int? penAway = null;
            if (apiStatus == "PEN" && fixture.Score?.Penalty is { Home: not null, Away: not null } pens)
            {
                penHome = pens.Home;
                penAway = pens.Away;
            }

            return (ninetyMinuteHome, ninetyMinuteAway, afterEtHome, afterEtAway, penHome, penAway);
        }
    }

    return (ninetyMinuteHome, ninetyMinuteAway, null, null, null, null);
}
```

In `Handle` (line ~50‑60) pass `fixture.Fixture!.Status.Short` and build the
richer DTO:

```csharp
var (homeScore, awayScore, afterEtHome, afterEtAway, penHome, penAway) =
    GetScoreForMatch(fixture, localMatch, isTournament, fixture.Fixture!.Status.Short);

return new MatchResultDto(localMatch.Id, homeScore, awayScore,
    GetMatchStatus(fixture.Fixture!.Status.Short),
    afterEtHome, afterEtAway, penHome, penAway);
```

### 6.3a `GetMatchStatus` — keep the score visible during ET/penalties

In the same handler, extend the status map so post‑90 in‑progress phases are
treated as `InProgress` (otherwise the score is cleared mid‑match — see §4.5):

```csharp
private static MatchStatus GetMatchStatus(string apiStatus) => apiStatus switch
{
    "FT" or "AET" or "PEN" => MatchStatus.Completed,
    "HT" or "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => MatchStatus.InProgress,
    "PST" => MatchStatus.Postponed,
    _ => MatchStatus.Scheduled
};
```

(`BT` = break before/within extra time, `P` = penalty shootout in progress,
`LIVE` = generic in‑play. `AET`/`PEN` remain the *finished* states that trigger
the result caption.)

### 6.4 `UpdateMatchResultsCommandHandler.cs` (line ~47)

```csharp
matchToUpdate.UpdateScore(
    matchResult.HomeScore,
    matchResult.AwayScore,
    matchResult.Status,
    matchResult.AfterExtraTimeHomeScore,
    matchResult.AfterExtraTimeAwayScore,
    matchResult.PenaltyHomeScore,
    matchResult.PenaltyAwayScore);
```

### 6.5 Admin "Enter Results" — preserve, don't wipe (phase 1)

The admin screen edits the 90′ score only. To avoid wiping sync‑captured ET/pens
when an admin re‑saves, the existing values **round‑trip** read‑only:

* `MatchViewModel.cs` — add read‑only properties initialised from the
  `MatchInRoundDto` (which now carries them, §6.6):

```csharp
public int? AfterExtraTimeHomeScore { get; } = match.AfterExtraTimeHomeScore;
public int? AfterExtraTimeAwayScore { get; } = match.AfterExtraTimeAwayScore;
public int? PenaltyHomeScore { get; } = match.PenaltyHomeScore;
public int? PenaltyAwayScore { get; } = match.PenaltyAwayScore;
```

* `EnterResultsViewModel.HandleSaveResultsAsync` (line ~49) — include them in the
  `MatchResultDto` so `UpdateScore` preserves them.

(Manual entry/editing of ET/pens by an admin is **out of scope** — see §9.)

### 6.6 DTO + queries that carry the actual result to the client

`MatchInRoundDto` feeds both grids. Append four nullable params **after** the
existing optional placeholder params:

`src/ThePredictions.Contracts/Admin/Rounds/MatchInRoundDto.cs`

```csharp
public record MatchInRoundDto(
    int Id,
    DateTime MatchDateTimeUtc,
    int? MatchNumber,
    int? HomeTeamId,
    string? HomeTeamName,
    string? HomeTeamShortName,
    string? HomeTeamAbbreviation,
    string? HomeTeamLogoUrl,
    int? AwayTeamId,
    string? AwayTeamName,
    string? AwayTeamShortName,
    string? AwayTeamAbbreviation,
    string? AwayTeamLogoUrl,
    int? ActualHomeTeamScore,
    int? ActualAwayTeamScore,
    MatchStatus Status,
    string? PlaceholderHomeName = null,
    string? PlaceholderAwayName = null,
    int? AfterExtraTimeHomeScore = null,
    int? AfterExtraTimeAwayScore = null,
    int? PenaltyHomeScore = null,
    int? PenaltyAwayScore = null);
```

Two query handlers populate it. **Mind the Dapper rules in `CLAUDE.md`:**

* **`GetMatchesForRoundQueryHandler.cs`** maps `QueryAsync<MatchInRoundDto>`
  **by name**, so just add the four columns to the `SELECT` (names must match the
  record params exactly):

  ```sql
  m.[ActualHomeTeamScore],
  m.[ActualAwayTeamScore],
  m.[Status],
  m.[PlaceholderHomeName],
  m.[PlaceholderAwayName],
  m.[AfterExtraTimeHomeScore],
  m.[AfterExtraTimeAwayScore],
  m.[PenaltyHomeScore],
  m.[PenaltyAwayScore]
  ```

* **`GetRoundByIdQueryHandler.cs`** builds `new MatchInRoundDto(...)`
  **positionally** from a private `RoundQueryResult` record. This is the one that
  throws at runtime if order drifts. Do all three in lockstep:
  1. add the four columns to the `SELECT` (after `PlaceholderAwayName`),
  2. add the four fields to `RoundQueryResult` **in the same order**,
  3. append the four args to the `new MatchInRoundDto(...)` call after
     `r.PlaceholderHomeName, r.PlaceholderAwayName`.

No other DTO needs the fields: `ActiveRoundMatchDto` (dashboard preview) shows
the *predicted* score only, and `PredictionScoreDto` is a prediction.

---

## 7. Per‑round legend (the "why")

Render a single muted note when the currently selected round contains at least
one match that went to extra time/penalties — derived from the data we now
carry, so no extra plumbing.

In **`RoundResultsTile.razor`**, just below the round‑pill selector (after line
~32) and inside the `selectedRound != null` block, add:

```razor
@if (State.CurrentRoundMatches.Any(m => m.AfterExtraTimeHomeScore.HasValue))
{
    <p class="knockout-scoring-note">
        <i class="bi bi-info-circle me-1"></i>
        Knockout matches are scored on the result after 90 minutes. Extra time and
        penalties are shown for reference only.
    </p>
}
```

CSS (add to `components/badges.css` or a small page rule — muted, centred,
small; provide a `.theme-dark` colour):

```css
.knockout-scoring-note {
    text-align: center;
    font-size: 0.75rem;
    color: var(--white-alpha-50);
    margin-bottom: 0.75rem;
}
```

(The tile sits on the purple dashboard background where text is light, hence
`--white-alpha-50`; verify contrast in both themes.)

---

## 8. Tests (required)

* **`MatchTests`** (`tests/Unit/ThePredictions.Domain.Tests.Unit/Models/MatchTests.cs`)
  — new/updated cases, named `MethodName_ShouldX_WhenY()`, using the public
  constructor with an explicit id, `CancellationToken.None` where relevant:
  * `UpdateScore_ShouldStoreExtraTimeAndPenaltyScores_WhenSupplied`
  * `UpdateScore_ShouldStoreExtraTimeScores_WhenNoPenalties` (pen args null)
  * `UpdateScore_ShouldClearExtraTimeAndPenaltyScores_WhenStatusScheduled`
  * `UpdateScore_ShouldClearExtraTimeAndPenaltyScores_WhenStatusPostponed`
  * `UpdateScore_ShouldThrow_WhenExtraTimeOrPenaltyScoreNegative` (covers each
    `HasValue` guard branch)
  * `Postpone_ShouldClearExtraTimeAndPenaltyScores`
  * Keep **100% line + branch**; run `tools\Test Coverage\coverage-unit.bat`.
* **`UpdateScoresForNextRoundCommandHandlerTests`** — `GetScoreForMatch`:
  * returns after‑ET + penalties for a `PEN` knockout fixture,
  * returns after‑ET, null penalties for an `AET` knockout fixture,
  * returns nulls for a 90′‑finished knockout (`FT`),
  * returns nulls for group‑stage / league matches.
* **`GetMatchStatus`** (§6.3a): `BT`, `P` and `LIVE` map to `InProgress`;
  `AET`/`PEN` stay `Completed`; unknown codes still fall back to `Scheduled`.
* **`MatchResultDtoValidator`** — if validation rules are added for the new
  fields (recommended: `InclusiveBetween(0, 30)` when `!= null` for penalties,
  `0–9` for after‑ET to mirror `HomeScore`), add matching tests in
  `MatchResultDtoValidatorTests.cs`. Penalty shootouts can exceed 9, so do **not**
  reuse the 0–9 rule for penalty columns.
* **Builders** — extend
  `tests/Shared/ThePredictions.Tests.Builders/Admin/Matches/MatchResultDtoBuilder.cs`
  with optional `WithExtraTime(...)` / `WithPenalties(...)`.
* Build with `/p:TreatWarningsAsErrors=true` to reproduce CI (xUnit1051 etc.).

---

## 9. Out of scope / optional follow‑ups

* **Admin manual entry of ET/penalties.** Phase 1 only round‑trips them. A
  later phase could add steppers to `EnterResults.razor` (and validation) so an
  admin can record/correct a shootout without the API. Document as a separate
  feature if wanted.
* **Showing AET/pens anywhere a *prediction* is rendered** — never; predictions
  are 90′ only.

---

## 10. Open verification item (carry over from scoring feature)

`tournament-knockout-scoring/README.md` still has this unchecked, and it gates
**both** scoring and this display:

> Verify with API‑Football 2022 World Cup data that `score.fulltime` returns the
> 90‑minute score **and** that `goals` returns the after‑extra‑time aggregate for
> `AET`/`PEN` fixtures (some providers populate `score.extratime` as ET‑only
> goals — we deliberately use `goals`, not `extratime`, for the after‑ET figure).

If real data shows `goals` is *not* the after‑ET aggregate for these fixtures,
adjust §6.3 to compute after‑ET from `score.extratime` instead — the rest of the
plan is unaffected.

---

## 11. Execution order (checklist)

1. [ ] DB: add the four columns (run §5.2 SQL); update `database-schema.md`.
2. [ ] Domain: `Match` properties + constructor + `UpdateScore` + `Postpone`; tests to 100%.
3. [ ] Contracts: extend `MatchResultDto` and `MatchInRoundDto`.
4. [ ] Application: `GetScoreForMatch` + `Handle` (capture); `GetMatchStatus` (§6.3a, BT/P/LIVE → InProgress); `UpdateMatchResultsCommandHandler` call site.
5. [ ] Queries: `GetMatchesForRoundQueryHandler` (by‑name) + `GetRoundByIdQueryHandler` (positional — lockstep!).
6. [ ] Admin round‑trip: `MatchViewModel` + `EnterResultsViewModel`.
7. [ ] UI: `MatchStatusBadge` (params + markup), wire `PredictionGrid` & `MobileMatchResultCard`.
8. [ ] CSS: `.match-result` / `.match-result-detail` (+ dark) in `components/badges.css`.
9. [ ] Legend: `RoundResultsTile.razor` + note CSS.
10. [ ] Validators + builders + tests; `dotnet build /p:TreatWarningsAsErrors=true` and coverage.
11. [ ] Manual verify: light + dark, desktop 24‑match grid wrap, mobile card, a `PEN` and an `AET` fixture, and that the 90′ score stays visible (pulsing) through `BT`/`ET`/`P` in‑progress states.
</content>
</invoke>
