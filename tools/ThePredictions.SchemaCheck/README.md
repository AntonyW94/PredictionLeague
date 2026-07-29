# ThePredictions.SchemaCheck

Checks every Dapper read in `src/` against the shape SQL Server actually returns, so result-set drift is
caught by a command rather than by a user hitting a broken page.

It exists because this class of bug is invisible to the compiler and to the unit tests: handler tests mock
`IApplicationReadDbConnection`, so no SQL ever runs. Two live examples it reproduces - a `long?` parameter
against an `int` column that broke the overall and monthly leaderboards, and a `CountInWindow` column that
never reached a `Count` property - both shipped green.

## Running it

```bash
dotnet run --project tools/ThePredictions.SchemaCheck
```

Reads the connection string from `PREDICTIONS_DEV_DB`, or pass `--connection "<connection string>"`. The
repository root is found by walking up for `ThePredictions.sln`; override with `--root <path>`.

Exit code is `1` when a read cannot materialise, otherwise `0`. Add `--strict` to fail on silent drops too.

**It never executes your SQL.** Every check goes through `sys.sp_describe_first_result_set`, which compiles
a statement and reports its result-set metadata without running it, so pointing this at a database holding
real data is safe - including for `INSERT`/`UPDATE` statements with an `OUTPUT` clause. A read-only login is
enough for `SELECT`s; statements that write will report a permission error and be skipped, which is fine.

## What it checks

Dapper fills a result type one of two ways, and the tool mirrors its actual selection logic
(`DefaultTypeMap.FindConstructor`): constructors are tried public-first then by ascending parameter count,
and a parameterless one wins as soon as it is reached.

| Mapping | When | Failure mode | Reported as |
|---------|------|--------------|-------------|
| **Positional** | No parameterless constructor - positional records, tuples | Throws at runtime | `Mismatch` / `Broken` |
| **By name** | A parameterless constructor exists, including a `private` one | Silently leaves members at their default and discards columns | `SilentDrop` |

For positional types, every parameter must line up with the column in the same position by name **and**
exact CLR type - `int` does not widen to `long`. For name-mapped types it reports columns with no settable
member and settable members with no column.

`Review` means a mismatch that only appears under one guessed parameter typing. Parameter types are read
off the anonymous object at the call site where possible (`nameof(...)` is a string, a numeric literal is an
int) because a guessed type changes the described type of a `CASE` expression that returns that parameter.
Anything a guess is involved in gets re-checked under the alternative typing before it is reported.

## Known limitations

All of these are **listed with a reason** in the `Skipped` section rather than passed over quietly.

- **Framework result types.** Types not declared under `src/` cannot be inspected, so a read into, say,
  `Microsoft.AspNetCore.Identity.UserLoginInfo` is skipped, not checked. That is a real gap: the
  `UserLoginInfo` defect fixed in #154 would have been *listed for review* by this tool, not flagged.
  Closing it means resolving those types by reflection over the referenced Identity assemblies.
- **`QueryMultiple` batches.** `sp_describe_first_result_set` only describes a batch's first statement, so
  the six `multi.ReadAsync<T>()` reads in `LeagueRepository` are skipped. They were checked by hand
  (entity members against table columns) when this tool was written.
- **Multi-mapping overloads.** `QueryAsync<TFirst, TSecond, TReturn>` splits the columns at `splitOn`; the
  tool does not model the split. The two in the repositories were checked by hand.
- **Non-constant SQL.** SQL that is not a compile-time constant cannot be resolved. Interpolated strings and
  concatenations built from constants *are* folded, as are `CommandDefinition`s held in a local.
- **Types with the same name in different files** resolve same-file-first, and are skipped if that is
  ambiguous rather than guessed at.

A baseline file for accepted `SilentDrop` results would let CI gate on new ones; today `SilentDrop` is
informational unless `--strict` is passed.

## Related

- [`docs/guides/database.md`](../../docs/guides/database.md#result-mapping) - the result-mapping rules this enforces.
- [`docs/todo/architecture/test-suite/README.md`](../../docs/todo/architecture/test-suite/README.md) - why the
  planned SQLite-backed query tests would not catch either failure mode.
