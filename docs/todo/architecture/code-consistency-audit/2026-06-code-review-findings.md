# Code Consistency Audit - Findings and Remediation Plan (June 2026)

## Status

Audit: **Complete** (performed 2026-06-12, full read-through of all projects)
Remediation: **Not Started** | In Progress | Complete

## How this audit was performed

Every file in Domain, Application, Infrastructure, API, Validators, Web.Client (auth and services) and Hosting.Shared was read in full. Contracts, the Razor components and the test projects were sampled and verified with solution-wide structural sweeps (one public type per file, single-line `if`, em dashes, `DateTime.Now`/`UtcNow`, US spellings, CQRS rule adherence, project reference graph, test naming).

Each finding below was reviewed with the product owner on 2026-06-12; the recorded **Decision** is binding for the remediation work.

## Overall verdict

The architecture is sound and the documented conventions are followed with unusual discipline: 2 single-line `if` statements in the whole solution, zero multi-type files in Contracts, 752/752 domain test methods following `Method_ShouldX_WhenY`. The findings below are edge drift, not structural rot.

---

## 1. Priority fixes (agreed)

### 1.1 Server-side FluentValidation never executes (the headline finding)

Every validator in `ThePredictions.Validators` targets a Contracts request DTO (`CreateLeagueRequest`, `RegisterRequest`, ...). The only server-side mechanism that runs validators is the MediatR `ValidationBehaviour`, which resolves `IValidator<TCommand>` - and no validator targets a command or query. The API registers the validators in DI but never enables auto-validation, and controllers do not invoke them manually. The rules therefore only run in the Blazor client's `FluentValidationValidator`, which a direct API call bypasses.

Practical impact: league name length and safe-name (XSS) rules, the 10,000 price cap, registration name sanitisation rules and password length checks are not enforced server-side except where a domain guard happens to overlap (and several do not - `League.Create` only guards against an empty name).

**Decision: gap - fix it.** The server must enforce the same rules as the browser.

- [ ] Choose the wiring: either add command/query validators so `ValidationBehaviour` does its job, or run the existing request validators at the API boundary (e.g. an action filter / endpoint filter invoking `IValidator<TRequest>`)
- [ ] Ensure every mutating endpoint's request passes through validation server-side
- [ ] Add a regression test proving an invalid request (e.g. 200-character league name with HTML) is rejected by the API without the client
- [ ] Update `docs/guides/cqrs-patterns.md` to document where validation runs

### 1.2 Winnings page prize pot omits the admin top-up (bug)

`GetWinningsQueryHandler` computes `TotalPrizePot = EntryCount * EntryCost`, ignoring `League.PrizeFundOverride`. The dashboard, homepage, my-leagues and pending-requests queries all include `ISNULL(l.[PrizeFundOverride], 0)`, so a topped-up league shows two different pot totals depending on the page.

**Decision: the Winnings page is wrong - include the top-up.**

- [ ] Add `PrizeFundOverride` to the league data read in `GetWinningsQueryHandler` and include it in `TotalPrizePot`
- [ ] Unit test covering a league with a top-up

### 1.3 ProcessPrizes runs each strategy once per prize setting

`ProcessPrizesCommandHandler` loops `league.PrizeSettings` and invokes the matching `IPrizeStrategy` per setting. Ranked categories have multiple settings (Overall ranks 1-3), so the same strategy runs several times, each run deleting and recreating the same winnings and re-fetching the league aggregate. Idempotent but wasteful, and a re-entrancy trap.

- [ ] Iterate `league.PrizeSettings.Select(p => p.PrizeType).Distinct()` instead
- [ ] Consider passing the already-loaded league into strategies to remove the per-strategy re-fetch

### 1.4 Guard clause message passed as parameter name

`Round.Validate` calls `Guard.Against.NegativeOrZero(seasonId, "Season ID must be greater than 0")` - the message string lands in the `parameterName` argument (the very next line uses the correct `parameterName: null, message:` form).

- [ ] Fix the call; sweep for any other two-argument guard calls where the second argument reads like a message

### 1.5 Non-deterministic prize distribution helper

`PrizeDistributionHelper.DistributePrizeMoney` distributes remainder pennies with `new Random()` (same inputs can pay different winners the extra penny on each run) and `(int)(totalAmount * 100)` can truncate. The domain's `PrizeApportionmentService.Distribute` already solves this deterministically.

- [ ] Replace the random remainder distribution with a deterministic top-down allocation
- [ ] Guard/round the pence conversion

---

## 2. CQRS and architecture decisions

### 2.1 Repositories in query handlers (violates CLAUDE.md rule 2)

Offenders: `UserOwnsLeaguesQueryHandler` (ILeagueRepository), `GetTournamentRoundMappingsQueryHandler`, `HasSeasonPredictionsQueryHandler`, `GetAvailableBoostsQueryHandler` (IBoostReadRepository).

- [ ] Convert these four query handlers to `IApplicationReadDbConnection` + SQL (the boost one may keep a read-model abstraction if renamed/considered part of the read side - decide during implementation)

### 2.2 Read connection in command handlers (violates CLAUDE.md rule 1)

`NotifyLeagueAdminOfJoinRequestCommandHandler` and `NotifyMemberOfLeagueApprovalCommandHandler` read user/season details via `IApplicationReadDbConnection`, deliberately avoiding the `[Leagues]` row locked by the in-flight join transaction.

**Decision: restructure rather than bless the exception.** Pass the data the notifications need into the notification commands (the join handler already holds the aggregate), so the handlers do not query at all.

- [ ] Extend `NotifyLeagueAdminOfJoinRequestCommand` / `NotifyMemberOfLeagueApprovalCommand` to carry recipient email/name and season name
- [ ] Remove the read-connection usage and the now-unneeded co-located DTOs (also fixes their one-type-per-file violations)

### 2.3 Google sign-in bypasses the user factory (violates CLAUDE.md rule 4)

`LoginWithGoogleCommandHandler` constructs `new ApplicationUser { ... }` directly because sanitised external names may legitimately be empty and `ApplicationUser.Create` would reject them.

**Decision: add a proper factory.**

- [ ] Add `ApplicationUser.CreateFromExternalProvider(...)` permitting empty names, with its own validation and tests; use it in the Google handler

### 2.4 Domain assembly ships to the browser (Contracts references Domain)

The WASM client transitively receives the full Domain assembly (prize maths, guard messages) because Contracts references Domain for shared enums.

**Decision: accepted trade-off.** No code change.

- [ ] Record the decision (this document suffices; optionally a line in `docs/guides/project-context.md`)

### 2.5 ApplicationUser inherits IdentityUser (framework dependency in Domain)

**Decision: accepted trade-off** - conventional for ASP.NET Identity. No code change; documented here.

### 2.6 Admin authorisation - defence in depth

Admin controllers all carry `[Authorize(Roles = Administrator)]`; only about half the admin handlers also call `ICurrentUserService.EnsureAdministrator()`.

**Decision: double-lock everywhere.**

- [ ] Add `EnsureAdministrator()` to the admin handlers missing it: UpdatePricingSettings, UpdateServiceFee, Create/Update/DeleteRunningCost, SendTestEmail, GetEmailTestDefaults/Templates (and sweep for any others under Features/Admin)
- [ ] Note in `docs/guides/cqrs-patterns.md` that admin command/query handlers must self-check

### 2.7 SyncSeasonWithApi is intentionally non-transactional

Round-by-round persistence keeps database locks short while calling a slow external API; the sync is re-runnable and self-healing.

**Decision: deliberate - document it.**

- [ ] Add a comment at the top of `SyncSeasonWithApiCommandHandler.Handle` explaining why it must NOT become `ITransactionalRequest`

### 2.8 Business rule inside LeagueRepository.CreateAsync

The "creator becomes an approved member" rule lives in the repository, invisible to domain tests.

- [ ] Move admin-member creation into the domain (`League.Create` or the command handler); repository persists what the aggregate holds

### 2.9 Mediator command chaining and TransactionBehaviour

Handler-to-handler `mediator.Send` orchestration (match results -> prize processing -> digest -> prize emails) **is the blessed house pattern - keep it**. Commands that take a `bool IsAdmin` flag from the edge (`DeleteLeagueCommand`, `GetManageLeaguesQuery`, `GetLeagueDashboardQuery`) should instead self-check via `ICurrentUserService`.

- [ ] Replace caller-supplied `IsAdmin` flags with `ICurrentUserService.IsAdministrator` in those handlers
- [ ] (Nice-to-have) Add an explicit rollback in `TransactionBehaviour`'s catch block rather than relying on dispose-time rollback

---

## 3. Entity hydration

**Decision: full public constructor is the canonical pattern** (matches `docs/guides/domain-models.md`).

- [ ] Add full public hydration constructors to: `LeaguePrizeSetting`, `UserPrediction`, `Winning`, `PrizeNotification`
- [ ] Align `LeagueRoundResult` and `RefreshToken` (remove public parameterless ctor styles; keep private ctor + full public ctor)
- [ ] Add the `Utc` suffix to `RefreshToken.Expires/Created/Revoked` (rename to `ExpiresAtUtc`, `CreatedAtUtc`, `RevokedAtUtc`; additive-then-destructive DbUp migration plus schema doc update)

---

## 4. Consistency drift (mechanical clean-ups)

### 4.1 IDateTimeProvider bypassed

Direct `DateTime.UtcNow` in: `SeasonPriceRecommendationService`, `UpdateScoresForNextRoundCommandHandler`, `GetActiveRoundsQueryHandler`, `GetWinningsQueryHandler`, `GetPredictionPageDataQueryHandler`, `CleanupExpiredDataCommandHandler`, `AuthControllerBase`. Also `CreateLeagueRequestValidator` / `UpdateLeagueRequestValidator` capture `DateTime.UtcNow` at construction time (use `.Must(d => d > DateTime.UtcNow)` so it evaluates at validation time).

- [ ] Inject `IDateTimeProvider` in the seven backend files
- [ ] Fix the two validators' deadline rules
- [ ] `UtcInputDate.razor` uses `DateTime.Now` (seeding a local-time input; replace or comment why it is exempt)

### 4.2 Em dashes (44 files)

Log templates (`PublishUpcomingRoundsCommandHandler`), comments, Swagger tags (`HomepageController`) and user-facing Razor text (Home.razor, Admin Edit pages) contain em dashes; the writing convention is plain hyphens.

- [ ] Sweep and replace `—` with `-` across src/tests/tools

### 4.3 UK English

- [ ] Rename `NameValidator.Sanitize` to `Sanitise` (and fix "sanitizes"/"users names" in its docs); update the two call sites in `LoginWithGoogleCommandHandler`

### 4.4 Exception taxonomy

- [ ] Not-found: standardise on `EntityNotFoundException` (replace `KeyNotFoundException` in `GetLeagueBankDetails`, `GetLeaguePaymentInfo`, `GetLeaguePayouts`; replace the Ardalis `NotFound` guard in `DeleteUserCommandHandler`)
- [ ] Authorisation: standardise on `UnauthorizedAccessException` (replace `AuthenticationException` in `DeleteLeagueCommandHandler`); decide whether the middleware should map it to 403 rather than 401
- [ ] Replace raw `throw new Exception(...)` in `DeleteUserCommandHandler` / `UpdateUserRoleCommandHandler` with `IdentityUpdateException`
- [ ] `ErrorHandlingMiddleware`: catch `OperationCanceledException` instead of matching `ex.Message.Contains("A task was canceled")`

### 4.5 Command/request shape

- [ ] Convert `ProcessPrizesCommand` and `UpdateAllLiveScoresCommand` to records; drop `IRequest<Unit>`/`Unit.Value` in favour of plain `IRequest` (`ConfirmEmail`, `RequestPasswordReset`, `ResendConfirmation`, `ProcessPrizes`)
- [ ] Move `CleanupResult` and `SecurityHeadersMiddlewareExtensions` into their own files (one public type per file)
- [ ] Fix the `IRequest<PredictionPageDto>` vs `IRequestHandler<..., PredictionPageDto?>` nullability mismatch in `GetPredictionPageData`

### 4.6 SQL convention drift

- [ ] Add table aliases where missing (`FetchAllTeams` all-teams branch, `GetTeamById`, `GetRunningCosts`, `GetUser`, `GetMyPayoutDetails`)
- [ ] Replace single-quoted aliases (`AS 'EntryDeadlineUtc'` in `GetLeagueById`, `AS 'MatchStatus'` in `GetRoundById`) with plain/bracketed aliases
- [ ] Parameterise hardcoded status strings (`'Approved'` in `GetHomepageSeasons`; `'Draft'/'Published'/'InProgress'/'Completed'` in `FetchAllSeasons` and `GetSeasonById`) using `nameof(...)` parameters like everywhere else
- [ ] Replace tab characters inside SQL string literals with spaces

### 4.7 Duplication (extract once)

- [ ] `ValidateSeasonAgainstApiAsync` (~50 lines duplicated between Create/UpdateSeason handlers) -> shared service
- [ ] The season-stats SELECT duplicated between `FetchAllSeasons` and `GetSeasonById` -> shared SQL constant or one handler
- [ ] `GenerateUrlSafeToken` duplicated (`EmailConfirmationSender`, `RequestPasswordResetCommandHandler`) -> shared helper
- [ ] `Ordinal()` implemented four times (PrizeApportionmentService, DigestEmailFormatter, Web.Client FormattingUtilities, plus suffix logic in components) -> consolidate where layering allows
- [ ] Site base-URL fallback string appears five times -> add `SiteSettings.ResolvedBaseUrl`
- [ ] `CountMonths` implemented three times -> shared helper
- [ ] Fixture-filtering block duplicated inside `SyncSeasonWithApiCommandHandler` (league vs tournament paths) -> extract

### 4.8 Culture handling

- [ ] `GetWinningsQueryHandler` and `GetMonthsForLeagueQueryHandler` use `CultureInfo.CurrentCulture` for month names (and the winnings one round-trips the month name through `ParseExact` to sort) - pin to en-GB like `PrizeNotificationFormatter` and sort by month number
- [ ] `AuthControllerBase` parses `RefreshTokenExpiryDays` with culture-sensitive `double.Parse` - use `CultureInfo.InvariantCulture`

### 4.9 Misc small items

- [ ] `new Random()` in `CreateLeagueCommandHandler.GenerateRandomEntryCode` -> `Random.Shared` (or `RandomNumberGenerator` for uniformity)
- [ ] Single-line `if` statements: `OverallPrizeStrategy` line 37, `DapperUserStore` line 199
- [ ] `RoundPrizeStrategy` / `MostExactScoresPrizeStrategy`: replace the `league?.X` then `if (league != null) { ... }` wrapper with early returns (match `OverallPrizeStrategy`)
- [ ] `IEmailService.SendTemplatedEmailAsync` lacks a `CancellationToken` (its sibling methods take one)
- [ ] `LeagueRoundResult.ApplyBoost` hardcodes the "DoubleUp" boost code - source it from the boost definitions/constants
- [ ] `CreateTeamCommandHandler` returns a DTO mixing `createdTeam.X` and `request.X` values - use the created entity throughout
- [ ] Remove the accidental `Microsoft.AspNetCore.Components.WebAssembly.DevServer` package references from the API and Infrastructure projects
- [ ] Local housekeeping (not a commit): delete the leftover `src/PredictionLeague.*` directories containing stale `obj/` artifacts from the project rename

---

## 5. Recorded decisions summary

| # | Question | Decision |
|---|----------|----------|
| 1 | Server-side validation gap | Fix - server must enforce all rules |
| 2 | Notify handlers using the read connection | Restructure - pass data into the commands |
| 3 | Google sign-up bypassing the factory | Add `CreateFromExternalProvider` factory |
| 4 | Domain assembly shipped to WASM client | Accepted trade-off |
| 5 | `ApplicationUser : IdentityUser` in Domain | Accepted trade-off |
| 6 | Admin authorisation placement | Double-lock: controller attribute AND handler check |
| 7 | Fixture sync non-transactional | Deliberate - document, do not "fix" |
| 8 | Winnings page prize pot | Bug - include `PrizeFundOverride` |
| 9 | Entity hydration pattern | Full public constructor everywhere |
| 10 | `IsAdmin` flags / command chaining | Convert flags to self-check; chaining stays the house pattern |

## 6. What the audit confirmed is in excellent shape (no action)

- Domain layer: factory + guard validation, write-once invariants, pure deterministic prize/pricing calculators with money-conservation guarantees, 100% coverage discipline
- CQRS adherence everywhere outside the handful of exceptions listed above
- Dapper positional-mapping discipline (co-located private result records with ordering comments)
- API layer: thin controllers, comprehensive Swagger annotations, per-IP rate limiting with a stricter auth bucket, constant-time API-key comparison, correlation IDs, security headers/CSP, careful cross-subdomain cookie handling
- Resilience: Polly retry on reads only, transient SQL fault classification, circuit breaker + timeout on the football API, live/ready health checks
- Blazor auth: serialised token refresh, transient-vs-terminal failure handling so deploys do not log users out, safe replay of bodyless requests only
- Security posture: versioned AES-GCM field encryption, enumeration-resistant password reset with rate limiting, audited cookie inventory in the consent service
- Tests: 752/752 domain test methods follow the naming convention; shared `TestDateTimeProvider` and builders

## Suggested implementation order

1. Section 1 (priority fixes - 1.1 first, it is security-relevant)
2. Sections 2.1-2.3, 2.6, 2.9 (agreed CQRS/authorisation changes)
3. Section 3 (hydration + RefreshToken rename, needs a migration)
4. Section 4 (mechanical sweeps - good batched "tidy" PRs)
