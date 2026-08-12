# 0017. SQL belongs to the persistence adapter

- **Status:** Accepted
- **Date:** 2026-08-12
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

`ThePredictions.Application` is where the business rules are supposed to live. It also held 331 `SELECT`s
across 53 files, and those statements were not only fetching rows - they were deciding things. Which of
two joint record-holders gets named. Whether a league is finished. Whether somebody may see a bank
account. Sixty-one `ISNULL(`, twenty-one `SELECT TOP`, nineteen `CAST(... AS bit)`, seventeen
`OUTER APPLY`, sixteen `GETUTCDATE()` and sixteen `RANK() OVER` sat in the layer that is meant to be
persistence-ignorant.

The interface boundary was already correct and machine-enforced: Application owned
`IApplicationReadDbConnection` and the repository interfaces, and `LayerDependencyConventionTests` failed
the build on a reference the wrong way. What leaked was the SQL text, and with it the rules.

Nothing tested those rules. A query handler holding a statement was excluded from coverage wholesale -
around 55 of them carried the same justification - on the reasonable grounds that a unit test would only
prove a mocked connection had received a string.

## Decision

Every read goes through **one interface per query, owned by Application and implemented in
`ThePredictions.Persistence.SqlServer`**. The adapter chooses rows; the handler decides what they mean.

Each statement was classified predicate by predicate against one question:

> Could someone who knows nothing about the business rules port this to another dialect correctly?

Yes, and it is mechanism - it stays in the adapter. No, and it is a rule - it moves to C#, with tests.
The operational form of the same question: **choosing which rows is fetching; computing what they mean is
a rule.**

Row types are Application-owned records, kept in lockstep with their `SELECT` (Dapper maps positionally).
Adapters get an **adapter-neutral conformance suite** - abstract tests naming what any implementation must
return - so a second adapter learns its obligations from the compiler rather than one failing test at a
time.

## Consequences

**For / positive**
- Application holds no SQL: 53 files to zero, and `SqlOwnershipConventionTests` fails the build if any
  returns. It strips comments while keeping string literals, because every converted file quotes the
  keywords it is describing, and a fourth test runs the same detector over the adapter so a sweep that
  stops looking fails rather than passing silently.
- Coverage exclusions on query handlers went from 59 to none. The handlers are measured, so the rules
  inside them are tested - including several that had never been executed by a test before.
- Reads are verified against the real database shape by `tools/ThePredictions.SchemaCheck`, which now also
  catches a nullable column read into a type that denies it.
- Around 90 query ports and 40-plus conformance suites; the integration suite went from 284 tests to
  roughly 500.

**Against / cost**
- More files. One interface, one row type and one adapter class per query is more to read than one handler
  with a statement in it.
- A positional coupling between each `SELECT` and its row type, which the compiler cannot see. SchemaCheck
  exists because of this and is not optional.
- Two trips where there was one, in the reads that used to join everything at once. Row counts were
  measured first; the largest is a few hundred rows.

**Neutral / notes**
- The write side followed. Set-based writes are mostly mechanism, but two were rules and moved:
  `Domain.Services.OutcomeTally` (how a player's predictions turned out) and
  `Domain.Services.LeagueScoring` (what a round is worth in a league, which existed **only** in SQL and so
  had nothing to disagree with it). `UpsertBatchAsync` and `UpdateLeagueRoundBoostsAsync` stay: they store
  values computed in C#.
- Seven write statements still call `GETUTCDATE()` rather than the injected clock. Agreed to move, not yet
  done.

## The rules this found duplicated

Each was one rule with two implementations and nothing linking them. This is the evidence the exercise was
worth its cost: none of these was visible while the SQL copy was unreadable to the tooling.

| Rule | Where it was doubled | Outcome |
|------|----------------------|---------|
| Predictable fixture | A round-completion query and the reminder service, each rewriting the composition in T-SQL | `Match.IsOpenForPrediction` |
| Round display name | The same two files, then two email reads that skipped the rule entirely | `Round.DisplayNameOrDefault` |
| Player display name | 17 files | `Domain.Services.PlayerDisplayName` |
| Where a player stands on the badges table | Page and tile disagreed on real data - joint players shared a position on one and not the other, and one awarded first place to accounts not on the table | `BadgeStandings` |
| Top scorer of a round | The digest email's tie-break could not separate two players sharing a first name, and disagreed with every leaderboard | `Ranking` |
| Longest run of rounds with an exact score | Two gap-and-island statements, differing only in scope | `Domain.Services.Badges.Streak` |
| Round outcome counts | The stored tally's `MERGE` and the active-rounds tile, untested in both | `Domain.Services.OutcomeTally` |
| The prize fund formula | Three copies, one on the least-watched page on the site | `Domain.Services.PrizeFund` |
| Whether a season has finished | Dashboards counted against the declared length, payouts against the rounds that exist; they disagreed | One definition, `SeasonCompletion.IsEveryRoundComplete` |

Four separate instances of "the column allows null and the result type denies it" were also found by hand,
one of which would have failed a screen outright on the first league saved without an entry deadline.
SchemaCheck now checks for that class of fault directly.

## Alternatives considered

- **Leave it and rely on integration tests.** Rejected: the rules would still have had no unit tests, and
  the duplicated ones would still have been invisible. Nine of the duplicates above were found only by
  reading each predicate on its own terms.
- **One interface per feature area rather than per query.** Rejected: a shared interface makes the
  fetch/rule line fuzzy again, and the conformance suite loses its focus.
- **A repository per aggregate for reads too.** Rejected: reads are shaped by their screen, not by the
  aggregate, and the CQRS split in this codebase deliberately keeps them apart (`docs/guides/database.md`).
