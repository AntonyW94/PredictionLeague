# Build Tooling: Raise AnalysisLevel To latest-recommended

## Status

Not Started | **In Progress (props + .editorconfig shipped; analysis-level raise outstanding)** | Complete

> **Shipped (2026-07-25):** the root **`Directory.Build.props`** and **`.editorconfig`**
> now exist. The props file sets `TreatWarningsAsErrors`, hoists `ImplicitUsings`/`Nullable`
> (removed from all 17 csprojs), and turns on `EnforceCodeStyleInBuild`; the `.editorconfig`
> encodes the conventions the codebase already follows (file-scoped namespaces, using
> placement, the naming split). A plain local `dotnet build` now fails on warnings exactly
> like CI, and the style/naming rules are enforced at build time. `AnalysisLevel` was left
> at the .NET 8 default (`latest`).
>
> **Outstanding:** raising `AnalysisLevel` to `latest-recommended` (the original Step 3).
> This was deliberately deferred - see below.

## Why the analysis-level raise was split out

Setting `<AnalysisLevel>latest-recommended</AnalysisLevel>` surfaces ~130+ analyser
findings that **cascade layer by layer** as each project is fixed
(Domain -> Application -> Web.Client -> Infrastructure -> API -> Web). It is a real,
multi-project code-change effort, not a tooling toggle, so it belongs in its own reviewed,
tested PR rather than buried in the "add the build files" change.

## Triage findings (from the 2026-07-25 attempt)

Most are mechanical and safe:

- **CA1860** (~50+) - prefer `Count == 0` over `Any()` on collections that expose `Count`.
- **CA1305 / CA1304 / CA1310 / CA1311** - culture on `ToString`/`Parse`/`ToUpper`/`ToLower`/`StartsWith`.
- **CA1861 / CA1825** - hoist constant array args to `static readonly`; use `Array.Empty<T>()`.
- **CA1869** - cache `JsonSerializerOptions` in a `static readonly` field.
- **CA1816** - call `GC.SuppressFinalize(this)` in `Dispose()` (several `IDisposable` types).
- **CA2201** - throw a specific exception type, not bare `Exception`.
- **CA1852** - seal internal types with no subtypes.
- **CA1806** - don't ignore a `Try...` return (`_ = ...`).
- **CA1822 / CA1725** - static test helpers; align override parameter names to the interface.

Handle with a **targeted suppression** (poor fit / churn), each justified inline or in props:

- **CA1716** - "Shared" in `ThePredictions.Hosting.Shared` collides with a reserved keyword;
  internal C#-only projects, renaming the namespace is pure churn.
- **CA1848** - `LoggerMessage` source-gen delegates; a micro-optimisation not warranted here.
- **CA1859** - prefer expressive `IReadOnlyList<T>` returns on private helpers.
- **CA1720** - `Status.Short` mirrors the football API's `status.short` field name.
- **CA1001** - `ApiAuthenticationStateProvider`'s `SemaphoreSlim` is held for the lifetime
  of the singleton; no meaningful disposal point in Blazor WASM.

**Needs care (do NOT rush):** the identity-normalisation rules **CA1304/CA1311** on
`DapperUserStore` (`ToUpper()`/`ToLower()` for `NormalizedUserName`/`RoleName`). Switching
culture can change the normalised values used for lookups against existing DB data - verify
`ToUpperInvariant()`/`ToLowerInvariant()` matches what is already stored before changing.

Test-project-only noise, scope to a `tests/Directory.Build.props` (import the root props,
then `NoWarn`): **CA1707** (underscored test names are the mandated convention), **CA1861**
(inline test-data arrays), **CA1826** (LINQ `First`/`Last` in assertions).

## Approach when picked up

1. Add `<AnalysisLevel>latest-recommended</AnalysisLevel>` to `Directory.Build.props`.
2. Build with `/p:TreatWarningsAsErrors=true`, collect and group errors by ID.
3. Fix the mechanical IDs; add the justified suppressions above (root props for product-wide,
   `tests/Directory.Build.props` for test-only); handle the identity culture rules carefully.
4. Rebuild until clean, run `dotnet test ThePredictions.sln` (and the Domain 100%-coverage
   run if Domain is touched during triage).
5. **Never** set `TreatWarningsAsErrors=false`. If genuinely unmanageable, the documented
   fallback is to stay on `latest`.

## Out of scope

- Custom Roslyn analysers, third-party analyser packages (StyleCop/Sonar/Roslynator).
- Central Package Management (`Directory.Packages.props`).
- Reformatting passes / line-ending / csproj indentation cleanup.
- Removing the `/p:TreatWarningsAsErrors=true` flag from the workflows (kept deliberately).
