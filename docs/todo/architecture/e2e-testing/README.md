# End-to-End Tests With Playwright

## Status

Not Started | **In Progress** | Complete

**The harness shipped in August 2026 and works.** `tests/E2E/ThePredictions.Web.Tests.E2E` builds a whole
stack per run on the CI runner - SQL Server container, production's schema from the committed DbUp
migrations, a seeded user, the published web application, real Chromium - and drives **one** journey through
it: a player signs in and reaches an authenticated dashboard. It runs on `e2e.yml`, gates pull requests, and
never touches dev or production. See
[`docs/guides/testing.md`](../../../guides/testing.md#end-to-end-journeys-against-an-isolated-stack).

One journey deliberately. The expensive part of a browser suite is the stack underneath it, and nothing about
it was proven until a single journey drove it end to end. **What remains is journeys, plus two decisions the
first of them forces** - see [Outstanding](#outstanding).

An earlier attempt targeted deployed dev instead, and was built and then abandoned; that decision record is
below, because it is the reason this looks the way it does.

## Priority

**High.** Two things put it here:

1. **The remaining backlog is mostly UX.** Prediction history, statistics dashboard, notifications UI,
   head-to-head, season recap, profiles, search, mini-leagues and the historical round view are all user
   journeys. Every one built without a test is debt nobody goes back for. Building the harness *before*
   that run of work means each feature can add its own journey test instead of accumulating a
   retrospective pile.
2. **A production incident on 2026-07-30 that nothing caught.** A leaderboard query's `SELECT` had
   drifted from its result record, so the page threw for a real user at 07:04 UTC. The compiler was
   happy, all 1,647 unit tests were green, and it was found by reading logs after the fact. A test that
   merely *loads a leaderboard* would have caught it.

---

## Decision: the deployed-dev smoke suite was built and abandoned (2026-08-14)

The original plan was two-stage: a small smoke suite against `dev.thepredictions.co.uk` first, needing
no seeder, then the isolated stack later. Stage 1 was built in full and went green in CI
([PR #277](https://github.com/AntonyW94/ThePredictions/pull/277), closed unmerged - the code is still
readable there and is the starting point for the work below).

**It was abandoned because dev is a working environment, not a test environment.**

Its data is changed constantly and deliberately while new work is tested, and it is periodically
replaced wholesale by a refresh from production. A suite asserting on it is aiming at a moving target
that a person is steering. The specific journeys above break if league 1 is deleted or renumbered (the
test accounts' membership goes with it), if season 1's dates move into the future (the league page
renders a countdown and no leaderboards at all), if its results are cleared (the leaderboard renders
its nothing-scored-yet state), or if the teams table is emptied. None of those is frequent. Each
produces a red run that is not a bug, and **a suite that cries wolf is worse than no suite**, because it
trains you to ignore it and is then not believed on the day it is right.

The positive half of the argument matters more. The isolated stack below has to be built regardless,
because it is the only thing that can reach the flows worth testing - submitting a prediction before a
deadline, entering results, prize payouts - and because running before a deploy lets it *gate* a change
rather than report on one that has already shipped. Given that, maintaining a second and weaker suite
against a moving target is not worth the upkeep.

> **A correction worth recording, because it was the original reason given and it was wrong.** This
> document previously asserted, and the abandonment was first argued on the basis, that `DataAnonymiser`
> invalidates every other password and therefore `testplayer@dev.local` is *the only account that can
> sign in on dev at all* - making it necessarily the same account used for manual testing. That is
> false. `DataAnonymiser.PreservedEmails` copies two real user rows through untouched, hashes included,
> and `PersonalDataVerifier` explicitly permits their `AspNetUserLogins` rows to survive, so the owner
> signs in as their own account and never touches the test accounts. There is no account collision.
> The decision to drop the suite was re-confirmed on the reasoning above once that was established.

### The narrower argument for it, and why it did not survive

The one thing only a deployed-site test can do is exercise the **real artefact on the real host**: IIS,
the published and minified CSS bundle, the FTP-uploaded file set, the Key Vault read `Program.cs` does
when `KeyVaultUri` is set. The isolated stack runs `dotnet run` on a Linux runner and sees none of it.

But that value needs no data assertions and no test accounts - loading the anonymous homepage proves
IIS is serving, the bundle is intact, Blazor WebAssembly boots and the API answers. A login-free
deploy check of exactly that shape was offered and **declined**: `deploy-dev.yml` already verifies a
200, checks the uploaded file count and warms the site, which covers the same ground closely enough to
not be worth a second workflow. Recorded here so it is not re-litigated, not because the door is shut.

### What the attempt established, and is worth keeping

Everything below was learned by building it, and applies directly to the work that remains.

**The Season Pass trap, which invalidated the original plan's premise.** The plan asserted that the two
dev test accounts, being league members, "can reach a dashboard and a leaderboard". They could not.
`get-pass` is a **required** step in `OnboardingStepRegistry`, so an account with no `SeasonPasses` row
has `RequiredComplete: false` and `/dashboard` renders the onboarding *takeover*: the checklist alone,
no tiles, no league link, no leaderboard. Confirmed against dev - `PassCount` was 0 for both accounts.

`TestAccountCreator` now grants each of `testplayer` and `testadmin` a free Standard pass for the season
of the league they join, and adds a third account, `testnewplayer@dev.local`, with deliberately no pass
and no league so the onboarding state is reproducible on demand.

**The seeder must handle the same trap.** A fixture that looks complete but leaves a required onboarding
step outstanding produces a page that is not the page under test, and it fails silently - the dashboard
renders, just not the dashboard you meant. Seed a `SeasonPasses` row for every user who is supposed to
be playing.

**Test framework: xunit.v3 with the base `Microsoft.Playwright` package.** Not
`Microsoft.Playwright.NUnit` or `.MSTest`. The auto-retrying web-first assertions (`Assertions.Expect`)
that make a browser suite stable ship in the **base** package; the per-framework packages add base
classes for the browser lifecycle, which is about a hundred lines to own outright. Pairing with NUnit
would have put a second test framework and idiom into a solution otherwise standardised on xunit.v3,
and would have collided with the "highest version including prerelease" rule, since NUnit is at
`5.0.0-beta.1` and the Playwright package does not support it. The cost is no `.runsettings` switches
for browser choice or headed mode; environment variables replaced them.

**Selectors verified against the running site**, so they do not have to be re-derived:

| Target | Selector | Verified |
|--------|----------|----------|
| Login email / password | `#email`, `#password` | Live |
| Login submit | `button[type='submit']` (exactly one on the page) | Live |
| Consent banner and its reject button | `.cookie-consent`, `.cookie-consent__btn--secondary` | Live |
| Anonymous home Login link | role `link`, name `Login`, **exact** (a "Login to Play" button also exists) | Live |
| Dashboard, either shape | `.dashboard-container` | Markup |
| My Leagues / Standings tiles | role `heading`, name `Leagues` **exact** (`Available Leagues` also exists) / `Standings` | Markup |
| Onboarding takeover | `.onboarding-card`, outstanding steps `.onboarding-step--active` | Markup |
| Open a league from the dashboard | `.my-leagues-card-actions .purple-accent-button` (label is "View Dashboard" or "View Recap" by season state, so do not match on text) | Markup |
| Any leaderboard row | `table.leaderboard-table tbody tr` | Markup |
| Admin team rows | `.team-list-grid .team-list-row` | Markup |
| An error panel anywhere | `.message-box-solid.error` - assert count 0 on every page | Markup |

**Four traps that cost real time:**

- **`.gitignore` silently swallowed the whole project.** Visual Studio's `*.e2e` trace-file pattern
  matches *directories* too, and `core.ignorecase` makes it case-insensitive, so a folder named
  `ThePredictions.Web.Tests.E2E` disappeared entirely - `git status` showed nothing. Un-ignoring
  `*.Tests.E2E/` is already committed, so the trap is disarmed.
- **Playwright .NET exposes no public `TimeoutException`** (1.62 ships only `PlaywrightException`), so
  `catch (TimeoutException)` binds to `System.TimeoutException` and never fires.
- **`LocatorIsVisibleOptions.Timeout` is `[Obsolete]`**, which is a build error under
  `TreatWarningsAsErrors`. `IsVisibleAsync` does not wait; use an explicit `WaitForAsync`.
- **A player who has finished the *required* onboarding steps still shows the onboarding card**, in its
  "You're all set" form, if any optional step is outstanding. Asserting "no onboarding card" fails on a
  perfectly healthy dashboard. Assert on the tiles instead - the takeover renders none.

**Timeouts.** 90s for navigation and 45s for assertions were needed against shared hosting, mostly for
the Blazor WebAssembly download and start. A local stack should be quicker, but do not tighten them on
faith.

**CI plumbing that will be needed again.** A `[Trait("Category", "E2E")]` on every test class, a
convention test that fails the build when one is missing (an untraited class runs in no job while CI
stays green), `coverlet.collector` but **not** `coverlet.msbuild` so the project stays out of the 100%
threshold it owns no assembly for, and the filter kept in step across `ci.yml` and both deploy
workflows.

---

## Outstanding

The stack, the levels, the CI wiring and the conventions are built. What is left:

### Decisions taken

- **Isolation: a season, league, player and round per test class.** Settled when the leaderboard journey
  arrived. Not a shared arrangement, because the first journey that **writes** breaks it - a test submitting
  a prediction breaks a test asserting none exist. Not Respawn either, which is how the integration suite
  gets its isolation: it deletes every row, and here a live application holds a connection pool over the
  database for the whole run. `TestDatabase.SeedLeagueAsync` takes the calling class's name and weaves it
  into the seeded player's email and the league's name, so classes cannot tread on each other. The run is
  still serial - one application process, and parallel WebAssembly browsers on a two-core runner would
  thrash - but that is now a property of the collection definition rather than of the data, so it can be
  revisited without touching a single assertion.

- **`SkiaSharp` on Linux: native assets added.** `ThePredictions.Infrastructure` now references
  `SkiaSharp.NativeAssets.Linux`, pinned to `4.150.1` to match the managed package **exactly** - the two
  halves ship in lockstep and a mismatch is a runtime failure, so this is the one place the highest-version
  rule in `CLAUDE.md` does not apply. `BadgeImageJourneyTests` exercises it, because adding a native
  dependency without running it would be a change made on faith, and the failure mode is lazy: the library
  loads on first use, so the application starts clean and then throws on the first request that draws.

  It also removes a latent production fault. Production is Windows/IIS today so the Linux binaries are dead
  weight there, but if the site is ever containerised the first deploy would boot fine and then fail the
  moment somebody pressed Share.

  **The cost was an order of magnitude worse than first estimated, and is worth knowing before touching it.**
  The package ships thirteen architectures - arm, arm64, musl, riscv, loongarch and the rest - which is
  **137MB across 13 files**, not the "few MB" it was pitched as. On an FTP transfer that already needs retry
  settings and a three-attempt file count, that is about 64% growth for something the host cannot execute.

  > **Correction, 2026-08-17.** Both deploy workflows briefly stripped `./publish/runtimes/linux-*` for that
  > reason, and this section claimed the strip was "safe by construction rather than by measurement: every
  > payload file in the package sits under `runtimes/linux-*`". The construction argument was wrong somewhere.
  > Dev returned **500.30, "failed to start", on two consecutive deploys** of that build, stayed down for
  > minutes rather than recycling, and came back up immediately on an identical deploy with only the strip
  > removed. The strip is gone from both workflows and the publish now goes up whole.
  >
  > **The mechanism is still unexplained**, and that is the useful part of this record. The same publish
  > starts fine locally on Windows against dev's own configuration, no stdout log was produced for any of the
  > crashed processes, and `Verify deployment file count` passed on every run including the failing ones. So
  > the reasoning above has not been shown to be false in its premises, only in its conclusion, which is
  > precisely why a narrower strip should not be reintroduced on the strength of a better-sounding argument.
  > If the upload size is worth solving, exclude the assets at publish time and prove it with a dev deploy.

  The **share card** remains out of scope and this does not change that. It draws text in seventeen places, so
  it needs fontconfig and fonts as well as the native library, and its correctness is visual rather than
  assertable. Badge glyphs are pure paths - nothing in that renderer touches `SKFont`, `SKTypeface` or
  `DrawText` - which is what makes the badge endpoint a clean probe.

### Journeys: every page, in fixture-dependency order

**The goal is all 47 routes.** The order is not alphabetical, and that is a deliberate decision rather than an
oversight - see [Why not alphabetical](#why-not-alphabetical).

Work in **layers**. Each layer is one extension to the fixture, and each unlocks a group of pages, so the
fixture is built once in the order its own dependencies demand rather than dragged into shape page by page.

| Layer | Fixture adds | Unlocks |
|-------|--------------|---------|
| **0** | a user *(done)* | anonymous pages, auth, account |
| **1** | season, competition, teams, league, membership, Season Pass, **one non-Draft round** *(done)* | dashboards, leagues, **leaderboards**, passes, badges |
| **2** | rounds and matches, every date relative to `UtcNow` | predictions, active rounds, admin rounds |
| **3** | results posted **through the admin endpoint**, so points and ranks are computed rather than seeded | round results, prizes, payouts, winnings, recap |

Layer 3's wording is the important one. Seed **inputs**, never **outputs**: a seeded rank makes a test that
asserts its own arrangement. Points, ranks and prize allocations must come out of the real write path -
`LeagueStatsRepository`'s 11 window functions - or the test proves nothing about them.

### The 47 routes

Layer 0 is available now. Nothing else is, until its layer's fixture exists.

**Layer 0 - a user, and that is all (13 routes)**

- [x] `/authentication/login` - signing in reaches an authenticated dashboard
- [ ] `/` - anonymous hero renders; an authenticated visitor is bounced to `/dashboard`
- [ ] `/authentication/register` - a new account is created and confirmation is demanded
- [ ] `/authentication/forgot-password` - a reset is requested and rate limiting bites on the fourth
- [ ] `/authentication/reset-password` - a valid token sets a password, an expired one refuses
- [ ] `/authentication/confirm-email` - a valid token confirms, a used one does not
- [ ] `/account/details` - name and phone save; marketing consent toggles both ways
- [ ] `/account/payout-details` - bank details save encrypted and read back for their owner only
- [ ] `/privacy`, `/terms`, `/cookie-policy`, `/licences` - render, and are reachable anonymously
- [ ] `/Error` - renders the branded 500 state
- [ ] Signing out returns to the anonymous site *(was written for the dev suite; port it)*

**Layer 1 - season, league, membership, pass (12 routes)**

- [ ] `/dashboard` - the settled dashboard: My Leagues and Standings tiles, no onboarding takeover
- [ ] `/dashboard` - the takeover, for a user with no pass and no league
- [x] `/leagues/{LeagueId:int}/dashboard` - **the overall leaderboard renders.** The shape of the 2026-07-30
      incident, and the highest-value journey in the list
- [ ] `/leagues` - lists the leagues the user can manage
- [ ] `/leagues/create` - a league is created, and the Season Pass gate refuses a user without one
- [ ] `/leagues/{LeagueId:int}/edit` - settings save
- [ ] `/leagues/{LeagueId:int}/members` - members list; a join request is approved and rejected
- [ ] `/leagues/{LeagueId:int}/prizes` - a prize scheme is configured
- [ ] `/leagues/{LeagueId:int}/preview` - the scheme is previewed against the current standings
- [ ] `/season-passes` - the acquire-first gate, and a £0 free-season acquisition
- [ ] `/badges` and `/badges/{UserId}` - the catalogue renders; another user's is viewable
- [ ] `/admin/users` - the user list renders *(admin, but needs no round state)*

**Layer 2 - rounds and matches, relative to `UtcNow` (9 routes)**

- [ ] `/predictions/{RoundId:int}` - **a prediction is submitted before the deadline, and refused after it.**
      The journey deployed dev could never have run
- [ ] `/admin/seasons` - the season list renders
- [ ] `/admin/seasons/create` - a season is created
- [ ] `/admin/seasons/edit/{SeasonId:int}` - a season is edited
- [ ] `/admin/seasons/{SeasonId:int}/rounds` - the round list renders
- [ ] `/admin/seasons/{SeasonId:int}/rounds/create` - a round is created with matches
- [ ] `/admin/rounds/edit/{RoundId:int}` - a round is edited, and a Draft round is **not** visible to a player
- [ ] `/admin/competitions` - the competition list renders
- [ ] `/admin/competitions/create` - a competition is created
- [ ] `/admin/competitions/edit/{CompetitionId:int}` - a competition is edited
- [ ] `/admin/teams` - the team list renders
- [ ] `/admin/teams/create` - a team is created
- [ ] `/admin/teams/edit/{TeamId:int}` - a team is edited

**Layer 3 - results through the real write path (8 routes)**

- [ ] `/admin/rounds/{RoundId:int}/results` - **an admin posts results and the leaderboard and rank cache
      move.** The one journey that proves the derived-state path end to end
- [ ] `/admin/rounds/{RoundId:int}/completion` - who has not predicted
- [ ] `/leagues/{LeagueId:int}/payouts` - payouts recorded against winners
- [ ] `/admin/seasons/{SeasonId:int}/pass-holders` - pass holders for a season
- [ ] `/admin/running-costs`, `/admin/pricing-settings` - render with data behind them
- [ ] `/admin/email-settings`, `/admin/email-tests` - render; **do not send anything**

**Deliberately not tested (5 routes)**

- `/authentication/external-login-callback` - Google OAuth; needs a real identity provider
- The **share card** endpoint - it draws text, so font rendering differs by platform and is not assertable.
  The **badge PNG** endpoint is now tested, since its glyphs are pure paths; see the note on `SkiaSharp` above
- Anything asserting an email arrived - Brevo is not called from the stack

### Why not alphabetical

It was considered, and the appeal is real: a complete enumeration with no judgement calls about what matters.
The numbers are what rule it out.

**20 of the 47 routes are `/admin/*`, and they sort first.** They are simultaneously the most fixture-hungry
in the application - seasons, competitions, teams, rounds, matches - and the least used. Alphabetical order
means building the heaviest fixture in the project to test `/admin/competitions/create` before login has a
second journey, and long before the leaderboard that actually broke in production.

**17 of the 47 take a route parameter**, so they cannot be visited at all without a seeded entity whose id the
test knows. Ordering by name ignores that entirely; ordering by layer is ordering by exactly it.

The completeness alphabetical was reached for is kept by the checklist above, not by the order of work.

### Smaller things worth doing when convenient

- **Nothing exercises `Level=Extended` yet.** `Core` is now covered by `JoinPrivateLeagueJourneyTests`, which
  proved the tickboxes and the matched-no-tests guard on a real selection; `Extended` has still never been
  selected, so that half of the filter remains untried.
- **`WebApplicationProcess`'s diagnostics are unproven.** It captures the application's output and detects
  early exit so a startup failure reports its reason rather than a bare timeout - but that path has never
  run, because startup has never failed. Worth deliberately misconfiguring once to check the report is
  actually readable.
- **The seeded user's `INSERT` duplicates `TestAccountCreator`'s**, so two places now know the
  `AspNetUsers` column list and a change to that table breaks both.
- **`DismissConsentBannerAsync` is documented as call-once-per-context but not enforced**; a second call
  waits 15 seconds for a banner already answered and then fails.
- **Why the stripped publish would not start on dev is unexplained.** Deleting `runtimes/linux-*` from the
  publish took dev down with 500.30 twice and removing the deletion brought it back, with no stdout log from
  any crashed process and the same publish starting cleanly on Windows locally. The workflows now upload the
  publish whole, so this costs about 64% on every FTP transfer until somebody understands it. Excluding the
  assets at publish time is the likely answer, but it needs a dev deploy to prove rather than an argument.
- **Nothing tests the deploy workflows themselves.** Two consecutive dev deploys of a build that could not
  start were reported green: `Verify deployment file count` passed, and `Warm up site` retried, touched
  `web.config`, waited 15 seconds and then ran a final `curl` whose status it never checks. A deploy that
  leaves the site returning 500 should fail, and this one cannot.

---

## The plan: isolated stack with a seeded database

### Shape of a single run

1. SQL Server **service container** (`mcr.microsoft.com/mssql/server:2022-latest`) starts on the runner.
   Developer edition is free for test use.
2. **DbUp builds the schema** from `src/ThePredictions.Persistence.SqlServer/Migrations/` - the same
   scripts production runs. Most projects hand-maintain a test schema; this one does not have to.
3. Seeder populates data (see below).
4. `dotnet run` the Web project against that connection string, in the background.
5. Playwright drives it, with `E2E_BASE_URL` pointed at localhost.
6. Job ends; everything vanishes.

Because it runs before anything ships, it can gate a PR rather than report after a deploy - which the
abandoned suite structurally could not.

### Most of the machinery already exists

The integration suite built it, and it is proven in CI:

| Piece | Where | State |
|-------|-------|-------|
| SQL Server per run | `Testcontainers.MsSql`, image pinned to `2022-CU14-ubuntu-22.04` | Done |
| Schema from the committed migrations | `Harness/MigrationRunner.cs`, 63 lines | Done |
| Row-level seeding | `ITestDataSeeder`, 33 methods, ~1,158 lines of implementation | Mostly done |
| Reset between tests | `Respawn` | Done |

`ITestDataSeeder` already covers users, competitions, seasons, teams, rounds, matches, leagues, members,
predictions, round results, boosts, prize schemes, payouts, badges, **season passes** and
`AddLeagueMemberStatsAsync`.

**This corrects the original plan, which treated the seeder as greenfield and argued that a
directly-seeding seeder could not produce ranks at all.** `AddLeagueMemberStatsAsync` writes the rank
cache directly, so the "no ranks on the dashboard" problem is avoidable without driving the write path.

The narrower version of that argument still stands and is worth keeping: seeding rank rows directly
means asserting against numbers you invented, not numbers `LeagueStatsRepository`'s 11 window functions
computed. For journey tests that is fine - proving the ranking is *correct* is the integration suite's
job. Use the API only where a journey is specifically about the recomputation, and prefer direct rows
everywhere else.

What genuinely remains: starting the Web project against the container, the relative-time round fixture,
supplying configuration without Key Vault, and the isolation decision.

### Test levels, and choosing them when you run it

Journeys are not equally worth running. Signing in is worth checking on every push; a rarely-touched
admin screen is not worth the minutes on every push but is worth checking before a release. So every
test class carries a **level** alongside its category trait:

| Level | Meaning | Examples |
|-------|---------|----------|
| `Smoke` | Cannot be broken without the site being unusable | Sign in, sign out, dashboard loads, a leaderboard renders |
| `Core` | Features used most weeks | Submitting a prediction, viewing a round's results, joining a league |
| `Extended` | Rarely used, but still has to work | Admin CRUD screens, prize payout flows, season recap, boost windows |

The level names describe the *test*, and the run names describe the *selection*: a smoke run takes
`Smoke`, a standard run takes `Smoke` and `Core`, a full run takes everything. Naming the third level
`Extended` rather than `Full` avoids the muddle of "the full run runs the Full tests plus the others".

Exactly one level per class, never several. The selection is built by combining levels in the filter,
so a test belonging to two would run twice in a standard run:

```bash
dotnet test --filter "Category=E2E&(Level=Smoke|Level=Core)"
```

`E2ELevelConventionTests` should fail the build when a test class has no `Level` trait, or carries a
value outside the three - the same shape as the category convention test, and for the same reason: a
class in no level is a class that runs in no job while CI stays green.

### Tickboxes when running it by hand

`workflow_dispatch` renders a `type: boolean` input as a checkbox, so the levels can be picked per run:

```yaml
on:
  workflow_dispatch:
    inputs:
      smoke:
        description: 'Run smoke journeys'
        type: boolean
        default: true
      core:
        description: 'Run core-feature journeys'
        type: boolean
        default: true
      extended:
        description: 'Run rarely-used-feature journeys'
        type: boolean
        default: false
  pull_request:
  schedule:
    - cron: '0 3 * * *'
```

Independent tickboxes rather than one dropdown, deliberately: "Extended only" is a genuinely useful
selection when you are working on a rarely-used feature and do not want to sit through the rest. A
`type: choice` dropdown of Smoke / Standard / Full is the simpler alternative if that flexibility turns
out not to be wanted.

**Build the filter in a shell step, not in a workflow expression.** This looks like it should work and
is broken:

```yaml
# WRONG - unticking the box silently turns it back on
SMOKE: ${{ inputs.smoke || 'true' }}
```

`false` is falsy in a GitHub expression, so `false || 'true'` evaluates to `'true'` and an unticked box
is indistinguishable from an absent one. The default has to be chosen by looking at the event instead:

```bash
if [ "${{ github.event_name }}" = "workflow_dispatch" ]; then
  SMOKE="${{ inputs.smoke }}"; CORE="${{ inputs.core }}"; EXTENDED="${{ inputs.extended }}"
else
  SMOKE=true; CORE=true; EXTENDED=false     # automatic runs: standard selection
fi

LEVELS=""
[ "$SMOKE" = "true" ]    && LEVELS="$LEVELS|Level=Smoke"
[ "$CORE" = "true" ]     && LEVELS="$LEVELS|Level=Core"
[ "$EXTENDED" = "true" ] && LEVELS="$LEVELS|Level=Extended"

if [ -z "$LEVELS" ]; then
  echo "No levels selected - tick at least one box."
  exit 1
fi

echo "filter=Category=E2E&(${LEVELS#|})" >> "$GITHUB_OUTPUT"
```

Three things worth knowing before relying on this:

- **The tickboxes only exist for manual runs.** `pull_request`, `push` and `schedule` have no UI, so
  their selection is hardcoded - which is the point: a PR gets smoke and core, the nightly cron gets
  everything.
- **`workflow_dispatch` allows at most 10 inputs**, so three levels leaves plenty of headroom.
- **Guard the empty selection.** With every box unticked the filter matches nothing, and `dotnet test`
  reports that far less clearly than the check above.

### Seeding through the application's own endpoints, where it earns it

```text
POST /api/admin/seasons/create
POST /api/admin/rounds/create
PUT  /api/admin/rounds/{roundId}/results   <- runs the real write path, populates the stats cache
```

Seeding through the system under test is circular: one broken write endpoint turns the whole suite red
with a single cause. Two cheap mitigations make that a reporting problem rather than a diagnostic one:

1. **Setup fails loudly and aborts the run** with the offending call named, e.g.
   `SEEDING FAILED: PUT /api/admin/rounds/12/results returned 500` - never twenty mystery downstream
   failures.
2. **A preconditions test runs first**, exercising only the endpoints seeding depends on. Red there means
   setup, not the feature under test.

### Do NOT containerise the website

A GitHub Actions runner is already an ephemeral, isolated environment destroyed after the run, so the
teardown is free. A Dockerfile would add an image to maintain and a build on every run for no test value.
Docker is needed for **SQL Server only**, because there is no other way to get one onto an Ubuntu runner.

**The `SkiaSharp` argument that used to sit here was wrong and has been corrected.** It said a Dockerfile
would force Linux-compatibility work because `ThePredictions.Infrastructure` references `SkiaSharp` and
`Svg.Skia` with no `SkiaSharp.NativeAssets.Linux` package (confirmed - just `SkiaSharp 4.150.1` and
`Svg.Skia 5.1.1`). True, but that is a **Linux** problem, not a Docker one: `dotnet run` on an
`ubuntu-latest` runner hits it identically, so avoiding the Dockerfile avoids nothing.

It is unlikely to block journeys - the dashboard's badge icons are client-side SVG and the PNG endpoint
serves emails - but it is an open decision rather than a settled one. Options: add the native-assets
package and fonts; run the site on a Windows runner and solve SQL Server differently; or accept that
image endpoints fail and keep them out of scope, which the "do not E2E the share-card image" note below
already implies.

(If the site is ever moved off shared hosting, containerising becomes worthwhile for its own reasons -
and would lift the APM ceiling in `../apm-integration/README.md`. That is a hosting decision, not a
testing one.)

### Time is the binding constraint

**Every seeded date must be computed relative to `DateTime.UtcNow` at seed time.**

- **`UtcNow`, never `Now`.** The SQL calls `GETUTCDATE()` in **22 places**, and the UK is UTC+1 for half
  the year. Seeding "deadline in one hour" with local time makes the database see two hours, so a test
  passes in January and fails in July. The root `CLAUDE.md` already bans `DateTime.Now`; it matters
  doubly here.
- **The clock cannot be pinned.** Those `GETUTCDATE()` calls are the *database's* clock, so registering a
  fixed `IDateTimeProvider` in the app does not affect them. Time-travel testing is off the table;
  relative offsets are the only option.
- Hard-coded dates in fixtures pass on the day they are written and fail silently forever after.

### Canonical round-state fixture

One season exercising nearly every branch, all relative to `UtcNow`:

| Round | Start | Deadline | Status | Purpose |
|-------|-------|----------|--------|---------|
| 1 | -14d | -14d | Completed, with results | leaderboards, stats cache |
| 2 | -1h | -1h | InProgress | live scoring |
| 3 | +2d | +2d | Published | the open prediction window |
| 4 | +9d | +9d | Draft | must NOT be visible |

Note the league page has a pre-season branch: while the season has not started it renders a countdown
and **no leaderboards at all**. Round 1 being in the past is what keeps the fixture out of it.

### Seeding belongs in setup, not in tests

A tempting design is "one test creates the season, another adds rounds". Do not do this. xUnit gives no
ordering guarantee and runs classes in parallel; one failure cascades into every downstream test, and a
single failing test cannot be re-run in isolation.

The *sequence* is right, its home is wrong: it belongs in fixture code (`IAsyncLifetime` on a class or
collection fixture). "Can an admin create a season?" still exists as a test - it just runs alongside
setup rather than being depended upon.

### Isolation - decide before writing tests

If every test shares one league, a test that submits a prediction breaks a test asserting none exist.
Either run the suite serially, or give each test class its own season and league. The second costs more
setup time but is far more robust, and with a seeder it is cheap enough to be worth it.

Note the abandoned suite ran serially for an unrelated reason - three fixed shared accounts - so its
collection definition is not a precedent to copy.

---

## Known blockers and frictions

| Friction | Severity | Notes |
|----------|----------|-------|
| Seeder design, especially relative time | **High** - the real cost, though lower than first assessed | Where most E2E suites rot |
| `SkiaSharp` has no Linux native assets | Medium, and undecided | Applies to the Ubuntu runner, not just a container. See above |
| Blazor WebAssembly load times | Medium | 90s navigation / 45s assertion timeouts were needed against shared hosting; a local stack should be quicker, but do not tighten on faith |
| Platform divergence (prod Windows/IIS, runner Linux) | Low-Medium | Fine for journeys; **do not E2E the share-card image** - font rendering differs |
| Key Vault configuration | Low | `Program.cs` reads Key Vault when `KeyVaultUri` is set; the test run must supply config directly |
| `TestAccountCreator` does not transfer | Low | It reads `SELECT TOP 1 ... FROM [Leagues]` to find a league and a season, so it presupposes a production copy. Useless on an empty database - the seeder must create the three account states itself, including the passes |

## Correction to the existing test-suite plan

`../test-suite/README.md` rates *Query Handler Integration Tests* as feasible on a **SQLite in-memory
database**. That premise never held - the queries are heavily T-SQL specific:

| Construct | Uses | SQLite |
|-----------|------|--------|
| `ISNULL(` | 73 | no |
| `SELECT TOP` | 29 | no |
| `GETUTCDATE()` | 22 | no |
| `CAST(... AS bit)` | 19 | no (no `bit` type) |
| `OUTER APPLY` | 17 | no |
| `SET TRANSACTION ISOLATION` | 4 | no |

That tier has since shipped against a real SQL Server container
(`tests/Integration/ThePredictions.Persistence.SqlServer.Tests.Integration`), which is the same
Testcontainers approach this plan needs.

## Open questions

- [ ] Serial suite, or a season and league per test class?
- [ ] Does it gate PRs, dev deploys, or both? If it gates a PR, does it gate on `Smoke` and `Core` only,
      with `Extended` left to the nightly run?
- [ ] Three independent level tickboxes as above, or one Smoke / Standard / Full dropdown?
- [ ] `SkiaSharp` on Linux: add the native assets, run on Windows, or keep image endpoints out of scope?
- [ ] Which three or four scenarios does the seeder have to produce first? The candidates are the ones
      deployed dev could never reach: submitting a prediction before a deadline, entering results as an
      admin, and a completed round's prize flow.
