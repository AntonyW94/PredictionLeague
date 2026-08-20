# 0016. Business rules get their own exception type; InvalidOperationException means server fault

- **Status:** Accepted
- **Date:** 2026-07-30
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

`ErrorHandlingMiddleware` mapped `InvalidOperationException` to **400 Bad Request and a Warning**, because handlers throw it for business rules ("Only pending members can be approved"). That made "client error" the default for a type the BCL and every library also throw for genuine defects.

The consequence showed up in production on 2026-07-29. `GetOverallLeaderboardQueryHandler` declared `long? SnapshotRank` against an `int` column, so Dapper could not materialise the result record and threw `InvalidOperationException`. The overall leaderboard was broken for every user in every league, and it was logged as a **Warning** behind a **400** - the same bucket as a validation failure. Nothing alerted, because nothing alerts on client errors, and the 400 told the caller they had made a mistake.

That was patched by translating read faults into `ReadQueryFailedException` inside `DapperReadDbConnection`. The patch was correct but incomplete: it covered one path, and the underlying rule was still fail-open. An audit found the same misfiling live on paths the patch never touched:

- **Repository reads.** `RepositoryBase` hands out a raw `IDbConnection`, so the ~20 Dapper reads in `src/ThePredictions.Infrastructure/Repositories/` had no translation. `BadgeEvaluationRepository` materialises five positional records (`UserCount`, `RoundUserResult`, `SocialiteAward`, …) through it - exactly the shape that broke the leaderboard. Its private generic `QueryAsync<T>` helper also defeats the `SchemaCheck` tool, which cannot resolve `T`, so those reads were neither statically verified nor correctly logged.
- **Missing configuration.** All 11 `InvalidOperationException` throws in Infrastructure are configuration faults, not business rules: absent Brevo API key, absent Stripe secret key and webhook secret, missing connection string, transaction misuse. Plus three in `FieldEncryptionService`, two missing Brevo template IDs, and a failed `refreshToken` cookie write in `AuthControllerBase`. Had the Stripe secret key gone missing in prod, checkout would have failed for every user as a Warning behind a 400.

Every new raw Dapper call or configuration guard was a fresh chance to misfile, because the safe outcome required remembering to opt out.

## Decision

We will invert the default.

- **`BusinessRuleViolationException`** (new, `ThePredictions.Domain.Common.Exceptions`) is the only type that means "a rule the caller could have satisfied". The middleware maps it to **400 and a Warning**. *(Severity revised by [ADR-0018](./0018-log-severity-says-who-must-act.md): client faults log at Information. The 400, and the decision about which type means a client fault, stand.)*
- **`InvalidOperationException` is no longer caught** by the middleware. It falls through to the unhandled bucket: **500 and an Error**, with a stack trace.

An unclassified fault is therefore reported as a server problem. That is the assumption that degrades gracefully: a business rule misfiled as a 500 is a cosmetically wrong status code on a path that was already refusing the request, whereas a defect misfiled as a 400 is silent breakage.

42 throw sites moved to the new type (9 in Domain, 33 in Application). 18 stayed as `InvalidOperationException` and are now correctly reported as server faults.

## Consequences

**For / positive**
- New infrastructure faults are filed correctly without anyone remembering to opt out.
- The 18 configuration faults above now produce Errors and 500s, so they reach error-rate alerting.
- Raw configuration messages ("Stripe secret key is …") stop being returned to clients, since the unhandled path returns a generic message outside Development.
- The type name states the intent at the throw site, so review can see a misclassification.

**Against / cost**
- A business rule that is missed in future returns a 500 instead of a 400. Cosmetically wrong, and the deliberate trade.
- `BusinessRuleViolationException` lives in Domain so Domain entities can throw it. Domain owning an exception the API maps to a status code is a mild layering compromise, consistent with the existing `SeasonPassRequiredException` and `EntityNotFoundException`.
- One-off churn across 42 throw sites and 19 test files.

**Neutral / notes**
- `ReadQueryFailedException` is kept. It no longer decides severity, but it names the failure in the log message rather than leaving a bare Dapper exception to be read.
- The repository-read gap is narrowed, not closed: a mismatch there is now logged correctly, but is still only caught at runtime. **Followed up:** `BadgeEvaluationRepository` now calls Dapper directly with concrete result types, so `SchemaCheck` resolves all 12 of its reads (they check clean). The remaining unchecked reads are the `QueryMultiple` batches and multi-mapping overloads listed in that tool's README.
- `ErrorHandlingMiddleware` had no unit tests, so this mapping was verified by reading it. **Followed up:** `tests/Unit/ThePredictions.API.Tests.Unit/Middleware/ErrorHandlingMiddlewareTests.cs` covers each branch, asserting log level as well as status code. Reinstating the old `catch (InvalidOperationException)` branch fails three of them.

## Alternatives considered

- **Translate at `RepositoryBase`** - a read helper that wraps Dapper's exception, with the ~20 direct call sites routed through it. Covers repository reads but stays fail-open: the next raw call or config guard misfiles again.
- **Sniff the message in the middleware** - match Dapper's "parameterless default constructor" text and escalate. Rejected: couples the middleware to a library's wording, and covers only the mismatch case.
- **Derive `BusinessRuleViolationException` from `InvalidOperationException`** - would let old `catch` clauses keep working, but a missed `throw` site would then still be caught by the 400 branch, which is the failure mode being removed.

## Related

- [ADR-0018](./0018-log-severity-says-who-must-act.md) - revises the severity half of this decision: client faults log at Information, and Warning is reserved for what somebody must act on.
- [`src/ThePredictions.API/CLAUDE.md`](../../src/ThePredictions.API/CLAUDE.md) - the exception-to-status table this defines.
- [`docs/guides/database.md`](../guides/database.md#result-mapping) - Dapper result mapping and read failures.
- [`tools/ThePredictions.SchemaCheck/README.md`](../../tools/ThePredictions.SchemaCheck/README.md) - the tool that catches materialisation drift before it ships, and its blind spots.
