# Component Logic Extraction

## Status

**Not Started** | In Progress | Complete

## Summary

Around 4,000 lines of logic live in Blazor `@code` blocks, where nothing can measure or test them,
because `**/*.razor` is excluded from coverage. Extract that logic into plain classes - following the
state-service pattern the codebase already uses - so it comes under the existing 100% gate. **This is
the largest remaining body of untested code in the repository.**

## Priority

**Medium.** Lower risk per line than the integration targets, because most of it is presentation, but by
far the biggest in volume. Should follow [`test-suite/high-risk-integration-targets.md`](../test-suite/high-risk-integration-targets.md),
which covers code that can lose data.

## Measured, August 2026

| Metric | Value |
|---|---|
| Components | 117 |
| With an `@code` block | 113 |
| Non-trivial `@code` lines | **4,026** |
| Components with 25+ lines | 59 |
| Branching constructs (`if`, `switch`, `??`, ternary, `foreach`) | **499** |
| `await` calls | 339 |
| `[Parameter]` / `[Inject]` declarations | 209 |

Concentrated, not evenly spread. The top four hold a large share of the branching:

| Component | Branches |
|---|---|
| `Pages/Predictions/Predictions.razor` | 45 |
| `Pages/SeasonPasses/SeasonPasses.razor` | 25 |
| `Pages/Dashboard/MyLeaguesTile.razor` | 22 |
| `Pages/Admin/Seasons/Edit.razor` | 21 |

For scale: that is more logic than everything covered during the August 2026 audit, which added roughly
250 tests across eleven types.

## Extract to services, not to code-behind

Three routes exist. The recommendation is the third.

| Route | Verdict |
|---|---|
| Adopt bUnit and test components as they are | Only route that tests *rendering*, but see below |
| Move `@code` to a `.razor.cs` code-behind | Measured, but keeps `[Inject]` property injection and lifecycle coupling |
| **Extract to plain, constructor-injected classes** | **Recommended** |

The codebase already does the third thing well: `DashboardStateService`, `LeagueDashboardStateService`
and `LiveScorePollingService` are ordinary classes with ordinary unit tests, and the components bind to
them. `BoostUsageSummaryBuilder` (August 2026) is the same move applied to a query handler.

Code-behind looks like the obvious answer and is worse than it appears: dependencies arrive through
`[Inject]` properties rather than a constructor, and those are usually private, so a plain test cannot
set them. A constructor-injected service has none of that friction.

**If logic is extracted, bUnit largely evaporates.** What is left needing it - `@if` branching in markup,
event wiring, parameter-change lifecycle - is a thin residue, and E2E covers rendering better anyway.
Reassess bUnit only after extraction, not before.

## The two phases cannot be separated

Moving logic out of a `.razor` file makes it **measured**, which means the 100% gate demands tests for it
in the same PR. A bulk move followed by a testing phase would leave CI red in between.

So each batch is: extract, test, merge. That paces the work naturally and keeps the gate meaningful
throughout.

## Requirements

- [ ] Work in risk order, not file order - `Predictions.razor` first
- [ ] Prefer extending an existing state service over creating a new one
- [ ] Each batch extract + test + merge in one PR, gate green throughout
- [ ] Components keep only binding and lifecycle wiring
- [ ] Reassess bUnit once the extraction is done, and record the decision either way

## A decision worth settling early

`**/*.razor` is currently excluded from coverage because the Razor compiler turns markup into
`BuildRenderTree`, which coverlet counted as ~4,200 uncovered lines - about 95% of Web.Client's coverable
lines, and roughly two thirds of every uncovered line in the solution.

If logic moves into plain classes, **that exclusion becomes permanent and correct**, rather than a
temporary compromise. `src/ThePredictions.Web.Client/CLAUDE.md` and `docs/guides/testing.md` currently
read as though it is a gap waiting to be closed. Once this plan is adopted, say so explicitly - markup is
not the unit suite's job.

The alternative, removing the exclusion so `BuildRenderTree` counts, means rendering every conditional
branch of all 113 components to reach 100%. That reintroduces the unreadable-report problem the exclusion
was added to solve.
