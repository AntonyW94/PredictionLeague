# Build Tooling: Directory.Build.props And .editorconfig

## Status

**Not Started** | In Progress | Complete

## Priority

**Medium.** Nothing is functionally broken: CI already builds the whole solution with `/p:TreatWarningsAsErrors=true`, so shipped code is warning-free. But that guarantee lives only on three workflow command lines - a plain local `dotnet build` (or an IDE build) does not enforce it, seventeen csproj files each repeat `ImplicitUsings`/`Nullable`, no Roslyn analysis level is pinned, and none of the documented style rules (`docs/guides/code-style.md`) are machine-enforced. This plan makes the local build match CI and encodes the conventions the codebase already follows. It is pure guardrail work with zero intended behaviour change, which is also why every step ends with a build that must come out clean.

All findings verified on 2026-07-06. **Where the code differs from this document, follow the code.**

## Verified current state

- There is **no** `Directory.Build.props` and **no** `.editorconfig` anywhere in the repository (glob-verified across the whole tree).
- **No csproj sets `TreatWarningsAsErrors`.** A repo-wide grep finds it only in the three workflows and in docs.
- CI enforces it on the command line only:
  - `.github\workflows\ci.yml` line 25: `dotnet build ThePredictions.sln --no-restore --configuration Release /p:TreatWarningsAsErrors=true`
  - `.github\workflows\deploy-dev.yml` line 55: same command
  - `.github\workflows\deploy-prod.yml` line 55: same command
- All **17** csproj files (9 under `src\`, 7 under `tests\`, 1 under `tools\`) individually set `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>enable</Nullable>`. No csproj sets `AnalysisLevel`, `AnalysisMode`, `EnforceCodeStyleInBuild` or `LangVersion`.
- `src\ThePredictions.Web.Client\ThePredictions.Web.Client.csproj` line 9 has `<NoWarn>$(NoWarn);NETSDK1198</NoWarn>` with an explanatory comment. **Keep it** - it is benign (publish profiles only exist in the Web server project) and correctly scoped to one project.
- Both `tools\ThePredictions.DatabaseTools` and every test project are **members of `ThePredictions.sln`**, so CI already builds them under `/p:TreatWarningsAsErrors=true` too.
- `global.json` pins the SDK to `8.0.100` with `rollForward: latestFeature`; nothing in this plan changes that.

**The key consequence:** because CI already builds the *entire solution* (including tools and tests) with warnings-as-errors, baking `TreatWarningsAsErrors=true` into a props file introduces **zero new errors** at the current analysis level. New errors can only appear when Step 2 (`EnforceCodeStyleInBuild` + `.editorconfig`) and Step 3 (`AnalysisLevel` raise) turn on rules that were previously off. That is why the steps are ordered the way they are, each with its own build-and-triage gate.

Style conventions the codebase demonstrably follows (sampled 2026-07-06; re-sample before encoding - see Step 2):

- **File-scoped namespaces everywhere**: a regex sweep of `src\**\*.cs` for block-scoped `namespace X {` declarations found zero matches; every namespace line ends with `;`.
- **`var` for locals throughout**: a sweep for explicit-type local declarations (`int x = `, `string x = ` etc.) found only record/method *parameters* with defaults, not locals.
- **Private instance fields are `_camelCase`** (e.g. `_mediator`, `_connectionFactory`), but private `const` and private `static readonly` fields are **PascalCase** (e.g. `RefreshTokenCookieName` in `AuthControllerBase.cs`, `PlaceholderRegex` in `ConfigurationSubstitutionExtensions.cs`). The naming rules below must respect that split or they will flag existing code.
- **Braceless single-statement `if` bodies on the next line** are house style (`docs/guides/code-style.md`, "Code Formatting"). So `csharp_prefer_braces` must NOT be enforced as true.
- **4-space indentation in `.cs` files.** Do not set indentation for XML/csproj files - the existing csprojs mix 2-space and 4-space indentation, and normalising them is churn this plan avoids.
- **UK English cannot be encoded** in either file; it remains a review/CLAUDE.md rule.

## Step 1: Root Directory.Build.props

Create `Directory.Build.props` in the repository root (beside `ThePredictions.sln`):

```xml
<Project>

  <!--
    Solution-wide build settings. Applies to every project under the repo root,
    including tests\ and tools\ThePredictions.DatabaseTools (all are in the solution
    and already build clean under these settings in CI, which passes
    /p:TreatWarningsAsErrors=true on the command line).

    Do NOT weaken TreatWarningsAsErrors here. Suppress a specific diagnostic with a
    targeted, commented NoWarn instead (see the suppression list below).
  -->
  <PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

**Decision - hoist `ImplicitUsings`/`Nullable`: yes.** All 17 csprojs already set both to `enable` with no exceptions, so hoisting changes nothing and removes 34 lines of drift surface (a future project cannot accidentally omit them). After creating the props file, delete the `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>enable</Nullable>` lines from all 17 csprojs:

- `src\ThePredictions.API\ThePredictions.API.csproj`
- `src\ThePredictions.Application\ThePredictions.Application.csproj`
- `src\ThePredictions.Contracts\ThePredictions.Contracts.csproj`
- `src\ThePredictions.Domain\ThePredictions.Domain.csproj`
- `src\ThePredictions.Hosting.Shared\ThePredictions.Hosting.Shared.csproj`
- `src\ThePredictions.Infrastructure\ThePredictions.Infrastructure.csproj`
- `src\ThePredictions.Validators\ThePredictions.Validators.csproj`
- `src\ThePredictions.Web\ThePredictions.Web.csproj`
- `src\ThePredictions.Web.Client\ThePredictions.Web.Client.csproj`
- `tests\Shared\ThePredictions.Tests.Builders\ThePredictions.Tests.Builders.csproj`
- `tests\Shared\ThePredictions.Tests.Shared\ThePredictions.Tests.Shared.csproj`
- `tests\Unit\ThePredictions.Application.Tests.Unit\ThePredictions.Application.Tests.Unit.csproj`
- `tests\Unit\ThePredictions.Composition.Tests.Unit\ThePredictions.Composition.Tests.Unit.csproj`
- `tests\Unit\ThePredictions.Domain.Tests.Unit\ThePredictions.Domain.Tests.Unit.csproj`
- `tests\Unit\ThePredictions.Validators.Tests.Unit\ThePredictions.Validators.Tests.Unit.csproj`
- `tests\Unit\ThePredictions.Web.Client.Tests.Unit\ThePredictions.Web.Client.Tests.Unit.csproj`
- `tools\ThePredictions.DatabaseTools\ThePredictions.DatabaseTools.csproj`

Leave everything else in the csprojs alone: `TargetFramework` stays per-project (explicit is clearer when a project eventually diverges), and per-project oddities like Web.Client's `NETSDK1198` NoWarn, `SatelliteResourceLanguages` in Web, `OutputType`, `IsPackable`, `InternalsVisibleTo` and `TreatAsUsed` metadata all stay where they are.

**Decision - scoping: one root file, no conditions.** Since CI already proves the whole tree (tools and tests included) is warning-free at the current analysis level, there is no reason to exempt `tools\` or `tests\` up front. If Step 2 or Step 3 triage reveals a flood of style/analyser errors that is genuinely test-only or tooling-only noise, do NOT weaken the root file; instead create `tests\Directory.Build.props` (or `tools\...`) that imports the parent and adds targeted relaxations:

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)..'))" />
  <PropertyGroup>
    <!-- Targeted, commented relaxations only - TreatWarningsAsErrors stays true. -->
    <NoWarn>$(NoWarn);CAxxxx</NoWarn>
  </PropertyGroup>
</Project>
```

(Note: a nested `Directory.Build.props` **replaces** the root one unless it imports it, hence the explicit `Import` line.)

**Verify immediately** (expected: zero errors, because this reproduces what CI already enforces):

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet build ThePredictions.sln
dotnet test ThePredictions.sln
```

The second (flag-less) build is the point of the exercise: it must now fail on warnings exactly like CI does. If either build errors here, something environmental is wrong (stale `obj\`, SDK mismatch) - run `git clean -xdf` on `obj`/`bin` folders and retry before touching any setting.

## Step 2: .editorconfig plus EnforceCodeStyleInBuild

**Goal: enforcement of what already holds, not churn.** Only rules the codebase already follows get warning severity (which `TreatWarningsAsErrors` then hardens into build errors); everything else is suggestion-or-below so it guides the IDE without breaking the build.

First, re-sample the conventions (they were sampled when this plan was written, but code moves):

```
# Block-scoped namespaces (expect: no matches)
grep -rnP "^namespace [^;]+$" src tests tools --include=*.cs

# Explicit-type locals that a var rule would flag (expect: only parameters/fields, not locals)
grep -rnP "^\s+(int|string|bool|decimal|double|DateTime) \w+ = " src --include=*.cs
```

Then create `.editorconfig` in the repository root:

```ini
# Top-most EditorConfig file for ThePredictions.
# Encodes the conventions the codebase already follows (docs/guides/code-style.md).
# Rules at :warning severity become build errors via TreatWarningsAsErrors +
# EnforceCodeStyleInBuild - only promote a rule to warning once the tree is clean for it.
root = true

[*.cs]
indent_style = space
indent_size = 4

# --- Namespaces: the tree is 100% file-scoped (verified by structural sweep) ---
csharp_style_namespace_declarations = file_scoped:warning

# --- using directives sit outside the namespace (universal in this codebase) ---
csharp_using_directive_placement = outside_namespace:warning

# --- var everywhere for locals (universal in this codebase) ---
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion

# --- Braces: single-statement bodies without braces are house style; do not fight it ---
csharp_prefer_braces = when_multiline:silent

# --- Formatting: never put the statement on the same line as the `if` ---
# (docs/guides/code-style.md "Code Formatting"). This option belongs to IDE0055,
# which is left at suggestion severity - see the note below the file.
csharp_preserve_single_line_statements = false

# --- Naming (docs/guides/code-style.md "Naming Conventions") ---
# Order matters: the first matching rule wins, so consts and static readonly
# (PascalCase in this codebase) must be matched before the general private-field rule.

dotnet_naming_symbols.constant_fields.applicable_kinds = field
dotnet_naming_symbols.constant_fields.required_modifiers = const
dotnet_naming_symbols.static_readonly_fields.applicable_kinds = field
dotnet_naming_symbols.static_readonly_fields.required_modifiers = static, readonly
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_symbols.interfaces.applicable_kinds = interface

dotnet_naming_style.pascal_case.capitalization = pascal_case
dotnet_naming_style.underscore_camel_case.required_prefix = _
dotnet_naming_style.underscore_camel_case.capitalization = camel_case
dotnet_naming_style.begins_with_i.required_prefix = I
dotnet_naming_style.begins_with_i.capitalization = pascal_case

dotnet_naming_rule.constants_are_pascal_case.symbols = constant_fields
dotnet_naming_rule.constants_are_pascal_case.style = pascal_case
dotnet_naming_rule.constants_are_pascal_case.severity = warning

dotnet_naming_rule.static_readonly_are_pascal_case.symbols = static_readonly_fields
dotnet_naming_rule.static_readonly_are_pascal_case.style = pascal_case
dotnet_naming_rule.static_readonly_are_pascal_case.severity = warning

dotnet_naming_rule.private_fields_underscore_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_underscore_camel_case.style = underscore_camel_case
dotnet_naming_rule.private_fields_underscore_camel_case.severity = warning

dotnet_naming_rule.interfaces_begin_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_begin_with_i.style = begins_with_i
dotnet_naming_rule.interfaces_begin_with_i.severity = warning
```

Deliberate omissions, so the executor does not "improve" them in: no `end_of_line` / `charset` / `trim_trailing_whitespace` / `insert_final_newline` under `[*]` (the tree is not consistent on these and normalising it is churn); no indentation rules for `.csproj`/XML (existing files mix 2- and 4-space); no using-directive *ordering* rule (the codebase places `System` usings last, a ReSharper convention that `.editorconfig` cannot express - `dotnet_sort_system_directives_first` only supports System-first, so leave ordering unenforced).

**IDE0055 note:** `csharp_preserve_single_line_statements = false` is a formatting option enforced by diagnostic IDE0055, whose default severity does not fail the build. Promoting IDE0055 to `warning` would enforce *all* whitespace/formatting options at once and will very likely flood. Leave it at its default in this step. Optional stretch: try `dotnet_diagnostic.IDE0055.severity = warning`, build, and keep it only if the error count is small enough to fix on the spot (the 2026-06 audit found only two single-line `if` statements - item 4.9 in [`../code-consistency-audit/2026-06-code-review-findings.md`](../code-consistency-audit/2026-06-code-review-findings.md)); otherwise revert the severity line and note it as a follow-up.

Then add `EnforceCodeStyleInBuild` to `Directory.Build.props` (this is what makes IDExxxx style diagnostics participate in the build at all):

```xml
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

**Verify** (required: zero new errors; if a naming or namespace rule fires, first check whether the flagged code genuinely deviates - fix the code if it is a one-off, downgrade that one rule to `suggestion` with a comment if the convention claim was wrong):

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test ThePredictions.sln
```

## Step 3: Raise the analysis level

Add to the `PropertyGroup` in `Directory.Build.props`:

```xml
    <AnalysisLevel>latest-recommended</AnalysisLevel>
```

**Why `latest-recommended`:** the .NET 8 default is `latest` with the *minimum* rule set (only a handful of CAxxxx rules enabled). `latest-recommended` enables Microsoft's recommended quality rules as warnings, which this repo's `TreatWarningsAsErrors` then enforces - that is the entire point of the exercise. Going further (`latest-all`) is not justified: it enables noisy/opinionated rules (globalisation annotations on every string call, etc.) that would demand a large NoWarn list and dilute the signal.

**IMPORTANT - triage is mandatory and expected.** New analysers WILL surface new warnings, and under this props file every one of them is a build **error**. Immediately after adding the line:

1. Run `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` and collect the full error list (build once with `-v:m` if IDs are truncated).
2. Group the errors by diagnostic ID and triage each ID:
   - **Fix trivially fixable ones** (typical finds: CA1861 avoid constant arrays as arguments, CA1854 prefer `TryGetValue`, CA2211 non-constant public static fields, CA1305 `IFormatProvider` on `Parse`/`ToString` - note the composition-root plan already removes the worst `double.Parse` offenders). Prefer the fix when it is mechanical and local.
   - **Suppress with a targeted NoWarn** when a rule is a poor fit for this codebase or the fix is genuinely out of scope. Every suppression goes in ONE place - the `Directory.Build.props` - as a documented list:

```xml
    <!--
      Analyser suppressions (AnalysisLevel latest-recommended), each justified:
        CAxxxx - <one line explaining why this rule is suppressed repo-wide>
        CAyyyy - <...>
    -->
    <NoWarn>$(NoWarn);CAxxxx;CAyyyy</NoWarn>
```

   - If a diagnostic only fires in test projects or `tools\ThePredictions.DatabaseTools` (analysers commonly flag test-style code: CA1707 underscores in names would be an example if it were enabled, disposal patterns in fixtures, etc.), scope the suppression with the nested `tests\Directory.Build.props` / `tools\Directory.Build.props` pattern from Step 1 rather than polluting the root list. Check both trees explicitly - `Directory.Build.props` applies to everything under the repo root, including `tools\`.
3. **Under no circumstances set `TreatWarningsAsErrors` to `false`**, globally or per-project, and do not remove the `AnalysisLevel` line to make errors disappear. If the error volume is unmanageable in one sitting, the acceptable fallback is `<AnalysisLevel>latest</AnalysisLevel>` (the current default, zero new rules) committed together with a note in this README that the raise to `latest-recommended` is still owed - but attempt the triage first; solutions of this size typically produce a triageable list.
4. Rebuild until clean, then run the full test suite:

```
dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true
dotnet test ThePredictions.sln
```

Known pre-existing interaction to be aware of (not caused by this plan): the `xunit.v3` analyser's **xUnit1051** already turns bare `default` CancellationTokens into CI failures; the tree is clean for it and must stay clean (always `CancellationToken.None` - see root `CLAUDE.md`).

## Step 4: CI and documentation notes

- **Keep the `/p:TreatWarningsAsErrors=true` flag in all three workflows** (`ci.yml` line 25, `deploy-dev.yml` line 55, `deploy-prod.yml` line 55). It is now redundant - the props file enforces the same thing - but it is belt and braces: it keeps CI red even if someone later edits or deletes the props file without understanding it. Add nothing, remove nothing in the workflows.
- Local builds now match CI: plain `dotnet build ThePredictions.sln` fails on warnings just like the pipeline. No developer-facing command changes.
- If the executor touches `docs\guides\code-style.md` for any reason, a one-line pointer ("these conventions are enforced by the root `.editorconfig`") is welcome but not required by this plan.

## Future extension (explicitly not part of this plan)

The Technical Notes section of [`../code-consistency-audit/README.md`](../code-consistency-audit/README.md) already floats the idea of a **custom Roslyn analyser** for the project-specific rules no off-the-shelf setting can express: missing SQL table aliases, direct `DateTime.UtcNow` instead of `IDateTimeProvider`, UK English spellings, and the single-line-`if` rule beyond what IDE0055 covers. That remains a separate future work item; this plan only wires up the built-in analysers and code-style enforcement.

## Out of scope

- Writing or adopting custom Roslyn analysers (see above).
- Third-party analyser packages (StyleCop.Analyzers, SonarAnalyzer, Roslynator) - evaluate separately once the built-in baseline has bedded in.
- Reformatting passes (`dotnet format`), line-ending/charset normalisation, or csproj indentation cleanup - this plan must not produce whitespace-only diffs in existing code.
- Changing `global.json`, the SDK version, `LangVersion`, or `TargetFramework`.
- Removing the `/p:TreatWarningsAsErrors=true` flag from the workflows (kept deliberately).
- Central Package Management (`Directory.Packages.props`) - worthwhile but a separate, riskier change.
- Fixing audit findings beyond those that happen to be flagged by the new analysers.

## Verification checklist

- [ ] `Directory.Build.props` exists at the repo root with `TreatWarningsAsErrors`, `ImplicitUsings`, `Nullable`, `EnforceCodeStyleInBuild`, `AnalysisLevel latest-recommended` (or documented fallback) and a commented suppression list (possibly empty)
- [ ] All 17 csprojs no longer declare `ImplicitUsings`/`Nullable`; Web.Client's `NETSDK1198` NoWarn is untouched
- [ ] `.editorconfig` exists at the repo root; every `:warning` rule in it is one the codebase already followed
- [ ] `dotnet build ThePredictions.sln` (no flags) fails on an artificially introduced warning and succeeds once it is removed (spot-check: add `int unused = 1;` to any method, observe the error, revert)
- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` clean
- [ ] `dotnet test ThePredictions.sln` fully green (including the Domain 100% coverage run via `tools\Test Coverage\coverage-unit.bat` if Domain code was touched during triage)
- [ ] Every NoWarn entry has a one-line justification comment beside it
- [ ] `TreatWarningsAsErrors` is `true` everywhere - no project, props file or workflow weakens it
- [ ] The three workflow files are byte-for-byte unchanged
