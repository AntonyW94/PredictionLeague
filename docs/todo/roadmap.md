# Year 1 Roadmap

## Overview

This roadmap is grouped into three tracks, each ordered by priority:

1. **Infrastructure** - CI/CD, resilience, testing, monitoring. The foundation everything else builds on.
2. **Quick Wins** - Low-effort items that must be done (legal, auth gaps, polish).
3. **New Features** - User-facing functionality that drives engagement and retention.

Items already completed are listed at the bottom. Work top-to-bottom within each track. Infrastructure items should generally be tackled before features at the same priority level, since they de-risk everything that follows.

**Plan status key:**
- **Detailed plan** - Full implementation plan with numbered subtasks exists in `docs/todo/`
- **Outline** - README with requirements and technical notes exists in `docs/todo/`
- **Idea** - No plan document yet

---

## Track 1: Infrastructure

*CI/CD, resilience, testing, monitoring, and developer workflow.*

| # | Item | Effort | Why | Plan |
|---|------|--------|-----|------|
| 1 | Machine-enforced conventions | Low-Medium | **Partly shipped, August 2026.** Delivered: `Microsoft.CodeAnalysis.BannedApiAnalyzers` banning `DateTime.Now`, `DateTime.Today` and `System.Data.SqlClient`, and `ThePredictions.Conventions.Tests.Unit` gating the layer graph (read from csproj `ProjectReference` elements, because the compiler elides unused assembly references), one public type per file, `Dto` only in Contracts, every `[ExcludeFromCodeCoverage]` carrying an approved `Justification`, no excluded type having a test file, and a burn-down allowlist of the six server-side `DateTime.UtcNow` reads. Every rule was verified to fail when violated. **Still outstanding: the Get/Fetch naming rule** (deliberately not enforced - the split is not actually documented anywhere in this repo, so it needs writing down first) **and the `SELECT *` rule** (eight reads: three in the Identity user store, two in the role store, two in the season repository, one in the refresh-token repository - the more dangerous of the two, since adding a column changes what the query hands the result type's constructor, exactly the runtime break ADR-0016 was written about) | Partly done |
| 2 | Supply-chain & dependency hygiene in CI | Low | Workflows pin actions by mutable tag (`actions/checkout@v6`), so a compromised tag is a live supply-chain hole. Nothing detects two projects referencing different versions of the same package, NuGet is not cached, and the DatabaseTools project (which executes against production) has no lock file | Idea |
| 3 | Assertion library: FluentAssertions to AwesomeAssertions | Low | Pinned to FluentAssertions **7.0.0**, the last free version - 8.x went commercial, so this dependency will never receive another update or security fix. AwesomeAssertions is the drop-in community fork; the migration is a namespace change across the test projects | Idea |
| 4 | Request timeout configuration | Low | No explicit timeouts currently = silent hangs in production | [Outline](architecture/request-timeouts/README.md) |
| 5 | Multi-result-set reads (`QueryMultipleAsync`) | Medium | `IApplicationReadDbConnection` exposes only single-result-set reads, so page-load handlers issue several sequential round trips. One batched read per screen cuts both latency and the lock-acquisition surface that causes the My Leagues spikes. Note it would put those batched reads outside what `SchemaCheck` can verify, so it pairs with real integration tests (#13) | Idea |
| 6 | `TimeProvider` instead of custom `IDateTimeProvider` | Low | The bespoke `IDateTimeProvider` predates the BCL's `TimeProvider`, which does the same job and brings a supported `FakeTimeProvider` for tests instead of a hand-rolled double | Idea |
| 7 | Football API resilience | High | Site completely fails if the API goes down. Circuit breaker + caching essential | [Outline](architecture/football-api-resilience/README.md) |
| 8 | Database resilience | Medium | Connection pooling + retry policies for shared hosting | [Outline](architecture/database-resilience/README.md) |
| 9 | Alerting configuration | Medium | Error, warning and uptime alerting shipped to Slack (see Already Complete); residual is response-time alerting, which needs a latency signal we do not yet collect | [Outline](architecture/alerting-config/README.md) |
| 10 | Working practices & operational knowledge | Low-Medium | Three gaps in the development loop: no single doc that reproduces every CI gate as a runnable local command (so a warning is first discovered as a red CI run), a security-audit checklist that nothing ever runs on a schedule, and no `.claude/skills` in the repo, so conventions like the PR format depend on being remembered. Also two operational practices worth writing down now the signals exist: **when a query regresses after a deploy that added indexes or columns, force the previous plan via Query Store** rather than reaching for `OPTION (RECOMPILE)` (which pays a compile cost forever and hides the next regression) or an index hint (which breaks silently on a rename); and **a `Warning` must mean "someone looks at this"** - if a fallback is normal operation it belongs at Information, and an intermittent third-party failure gets a retry policy before anyone downgrades its log level, so a genuine our-side fault still alerts on first occurrence | Idea |
| 11 | Caching strategy | High | Every request currently hits the database. Leaderboards/teams/seasons are obvious cache targets | [Outline](architecture/caching-strategy/README.md) |
| 12 | Pagination | Medium | Needed once you have real data volumes in leagues and leaderboards | [Outline](architecture/pagination/README.md) |
| 13 | Query handler integration tests (Phase 3) | Medium | Catches SQL mapping bugs. Highest-value test tier after domain tests. **The plan's SQLite in-memory choice needs revisiting** - SQLite cannot evaluate the `RANK() OVER`, `CROSS APPLY`, `MERGE` and `CAST(... AS bit)` this codebase depends on, so those tests would pass against SQL the real database rejects. Build the schema by running the DbUp migrations into a throwaway SQL Server container (Testcontainers), which also gives continuous proof the migration set builds a working schema from scratch. Worth adding reflection gates so a new handler or repository without a test fails the build | [Detailed plan](architecture/test-suite/README.md) |
| 14 | Command handler unit tests (Phase 5) | Medium | Test business logic orchestration with mocked repositories | [Detailed plan](architecture/test-suite/README.md) |
| 15 | Code consistency audit | Medium | Clean up tech debt now that patterns are established | [Outline](architecture/code-consistency-audit/README.md) |
| 16 | Query performance monitoring | Medium | Slow-query logging shipped (see Already Complete); remaining work is reviewing logged slow queries to add missing indexes | [Outline](architecture/query-monitoring/README.md) |
| 17 | E2E tests with Playwright | High | **Moved up (2026-07-30).** The remaining backlog is mostly UX journeys, so building the harness first means each feature adds its own test rather than accruing retrospective debt - and a leaderboard broke in production on 30 July with 1,647 green unit tests. Stage 1 is a smoke suite against dev needing no seeder | [Detailed plan](architecture/e2e-testing/README.md) |

> **Items 1, 2, 3, 5, 6 and 10 were added on 2026-08-03** from a review comparing this solution
> against the EctManager core tree (`E:\Git\EctManager`), a multi-developer .NET 8 conversion by the
> same developer. They have no plan documents yet (status **Idea**). Their numbers put them above
> #4 in the table, which reflects the review's recommended priority rather than an agreed
> re-prioritisation - reorder them freely. The same review sharpened #13 (integration-test database
> choice) and fed two amendments into existing plans:
> `composition-root-and-hosting` (validate options at start-up) and `transaction-context-hardening`
> (`RepositoryBase` opens a fresh connection on every property access outside a transaction).

---

## Track 2: Quick Wins

*Legal requirements, auth gaps, security, and polish. Low-to-medium effort items that need doing.*

| # | Item | Effort | Why | Plan |
|---|------|--------|-----|------|
| 18 | Copyright footer | Trivial | Needed before any public users see the site | [Outline](features/legal-compliance/copyright-footer/README.md) |
| 21 | Privacy & terms pages | Low | Legal requirement for any site collecting user data | [Outline](features/legal-compliance/privacy-terms-pages/README.md) |
| 22 | Signup legal checkboxes | Low | Legal requirement; depends on #21 existing first | [Outline](features/legal-compliance/signup-legal-checkboxes/README.md) |
| 23 | Email verification | Medium | DB column exists but no verification email or token flow yet. Must know emails are real | [Outline](features/authentication/email-verification/README.md) |
| 24 | Cookie consent | Medium | GDPR mandatory before public launch in the UK | [Outline](features/legal-compliance/cookie-consent/README.md) |
| 25 | Audit logging | Medium | Track who did what, for security and debugging | [Outline](security/audit-logging/README.md) |
| 26 | Data export (GDPR) | Medium | GDPR right to data portability | [Outline](features/legal-compliance/data-export/README.md) |
| 27 | Deferred security items | Medium | SameSite=Strict on refresh cookie (accepted risk), open redirect fix. Clean up once login system is stable. JWT ClockSkew + algorithm allow-list already shipped. | [Outline](security/) |

---

## Track 3: New Features

*User-facing functionality that drives engagement and retention.*

| # | Item | Effort | Why | Plan |
|---|------|--------|-----|------|
| 30 | User onboarding | Medium | Reduce drop-off for new signups | [Outline](features/user-experience/user-onboarding/README.md) |
| 31 | Email preferences | Medium | Let users control what they receive | [Outline](features/email-notifications/email-preferences/README.md) |
| 32 | Notifications UI | Medium | Dashboard alerts tile exists; extend to bell icon and dropdown for general notifications | [Outline](features/user-experience/notifications-ui/README.md) |
| 33 | Accessibility (WCAG basics) | Medium | Right thing to do, also a legal consideration | [Outline](features/user-experience/accessibility/README.md) |
| 34 | Historical round view | Medium | Selecting a past round on a league dashboard currently changes only the round-results grid. This makes the whole dashboard - standings, boosts, prizes - show the league as it stood at the end of that round. Mostly a query change rather than a schema one, since `LeagueRoundResults` already holds per-round points. Distinct from #35, which is a player's own prediction history rather than the league's past | [Detailed plan](features/user-experience/historical-round-view/README.md) |
| 35 | Prediction history | Medium | Data exists in DB and is shown in league dashboards; needs a dedicated history view | [Outline](features/user-experience/prediction-history/README.md) |
| 36 | League notifications | Medium | Join request notifications exist; extend to broader league events | [Outline](features/email-notifications/league-notifications/README.md) |
| 37 | Admin dashboard | Medium | Admin CRUD pages exist; need a summary/overview page with stats | [Outline](features/admin-moderation/admin-dashboard/README.md) |
| 38 | Statistics dashboard | Medium | Some stats exist across league dashboards; needs a unified personal stats page | [Outline](features/user-experience/statistics-dashboard/README.md) |
| 39 | Season recap | Medium | End-of-season summary. Shareable, fun | [Outline](features/user-experience/season-recap/README.md) |
| 40 | Head-to-head comparison | Medium | Social/competitive feature | [Outline](features/user-experience/head-to-head/README.md) |
| 41 | Social sharing (per-content OG) | Low | Image share of predictions shipped (see Already Complete); remaining work is per-content Open Graph/Twitter card images, which need crawler-facing server-rendered meta tags in the Web host | [Outline](features/user-experience/social-sharing/README.md) |
| 42 | Digest emails | Medium | Weekly summaries for less active users | [Outline](features/email-notifications/digest-emails/README.md) |
| 43 | League moderation | Medium | Basic member management exists; extend to full moderation tools | [Outline](features/admin-moderation/league-moderation/README.md) |

---

## Backlog (Year 2+)

These are parked. They're low priority, depend on scale, or are nice-to-haves.

### Features
- Dark mode
- PWA support
- Offline support
- League chat
- Search functionality
- Monthly leaderboard scenarios
- Public profiles
- Prize summary badges
- Help documentation

### Auth
- Two-factor authentication
- Account recovery
- Multi-device sessions

### Architecture
- Read replicas
- Data archiving
- Dead letter queue
- CDN for static assets
- Full APM integration

### Admin
- Content moderation
- Report management
- System announcements
- Support tools
- Bulk operations

### Security
- Suspicious activity detection
- Admin IP protection
- API key rotation
- Penetration testing

---

## Already Complete

These items from the original backlog are already implemented:

| Item | Notes |
|------|-------|
| Session management | JWT + refresh tokens, 60min/7day expiry |
| Password requirements | ASP.NET Identity: 8 char min, digit, uppercase, lowercase, 4 unique chars |
| Password reset | Full flow: token generation, rate limiting (3/hr), expiry, Google user handling, auto-login after reset, Blazor pages |
| Account lockout | Configured: 5 failed attempts = 15 min lockout |
| Google OAuth | Full implementation: login, callback, account linking, token generation |
| Transactional emails | Brevo integration with templates for password reset, league join, reminders |
| Email templates | Brevo templated emails already in use across all email types |
| Prediction reminders | Smart milestone reminders (5d, 3d, 1d, 6h, 1h), deduplication, scheduled endpoint |
| Mobile responsive design | Mobile-first CSS, 4+ breakpoints, 70+ responsive CSS files |
| Form validation (all forms) | FluentValidation on both public and admin endpoints (28 validators) |
| Loading states | Per-component `IsLoading` pattern, CSS spinners, auth loading state |
| Request security headers | `SecurityHeadersMiddleware`: CSP, HSTS, X-Frame-Options, X-Content-Type-Options, Permissions-Policy, Referrer-Policy |
| User profile | Account details page with edit for name/phone, GET/PUT `/api/account/details` |
| Leaderboard enhancements | Multiple types (overall, monthly, exact scores, winnings), rank change arrows, snapshot tracking |
| User management (admin) | Full CRUD: list users, update roles, delete with league ownership transfer |
| Mini leagues | Private leagues with 6-character entry codes, join-by-code flow, league discovery |
| Live scores | Scheduled every-minute polling of football API, updates match scores during live windows, shows on dashboard |
| Staging / dev environment | Live at `dev.thepredictions.co.uk`, uses dev database |
| Domain unit tests (Phase 1) | 462 tests, 100% line and branch coverage |
| Dev DB refresh workflow | GitHub Actions, manual trigger |
| Prod backup workflow | GitHub Actions, daily at 2am UTC |
| CI workflow (`ci.yml`) | Build + test on every push/PR with Coverlet code coverage |
| Deploy workflows (`deploy.yml`) | One-click deploys to dev and production via FTP with verification and warm-up |
| Tournament support | Schema, domain models (CompetitionType, TournamentStage), sync handler, placeholder matches, round mappings, API endpoints, UI CSS, 50+ tests. 90-minute knockout scoring complete; knockout ties are shown as their scored 90-minute result (AET/penalty detail intentionally not displayed) |
| Validator tests (Phase 2) | `ThePredictions.Validators.Tests.Unit`, ~261 cases across all validators |
| Round results digest emails | Per-user post-round digest (Brevo template 11), per-league links, admin resend, idempotent via `Round.ResultsDigestSentUtc` |
| Prize-won notifications | Celebratory email to winners (Brevo template 12), idempotent via `PrizeNotifications` sent-log, admin resend |
| Email test tool (admin) | `/admin/email-tests` page with live Brevo template discovery and smart parameter defaults |
| Database migrations (DbUp) | DbUp migrator + numbered embedded scripts + per-env migrate workflows (ADR-0013). Plan removed. |
| Achievement badges | Badge catalogue, awarding engine (hooked into match-result updates), backfill command, and display. Plan removed. |
| Live score auto-update | Client-side visibility-aware 10s polling on the league and user dashboards (`LiveScorePollingService`). Cache-backed reads + stale banner deferred to caching/resilience work. Plan removed. |
| Incomplete predictions visibility | Round-completion query, admin completion page, dashboard tile, reminder wiring. Plan removed. |
| Season Passes (Standard launch) | Standard tier live on prod: Stripe Checkout + webhook fulfilment, acquire-first gate + free/trial flow, running-costs calculator, email-confirmation gate (ADR 0014). Only the deferred SMS/Premium tier + self-service refunds remain (see `features/monetisation/season-passes/`). |
| JWT hardening (ClockSkew + algorithms) | Explicit 30s `ClockSkew` + `ValidAlgorithms` allow-list (HmacSha256) on `TokenValidationParameters`. Plan removed; SameSite=Strict on the refresh cookie remains an accepted risk. |
| Origin-header email links | Emailed links (password reset, email confirmation, league notifications) now build from configured `SiteSettings.ResolvedBaseUrl`, never the attacker-controllable `Origin` header. Closes the reset-link poisoning / account-takeover vector; consolidates the duplicated base-URL fallback. Plan removed. |
| Drop RoundResults.TotalPoints | DbUp migration `0005_DropRoundResultsTotalPoints.sql` drops the vestigial, unread global points column (points are per-league in `LeagueRoundResults`). Schema doc updated; refresher is column-agnostic so needs no change. Plan removed. |
| Slow-query logging | Read queries slower than a configurable threshold (`QueryMonitoringSettings`, default 500ms) are logged at Warning via `DapperReadDbConnection`. The query-monitoring plan is trimmed to the remaining missing-index review. |
| Third-party licences page | `/licences` page (linked in the footer) lists the distributed open-source packages and their licences, generated from each package's NuGet `.nuspec` licence metadata. Plan removed. |
| Marketing opt-in toggle | Users can change (and revoke) marketing consent on `/account/details`; folds into the details save via `ApplicationUser.SetMarketingOptIn`, pre-populated from `MarketingOptInAtUtc`. Plan trimmed to the remaining Google-signup capture. |
| Reminder email urgency params + template | The reminder handler sends `PREDICTIONS_URL`, `URGENCY` and `TIME_REMAINING` (`ReminderUrgencyFormatter`); Brevo template 9 was redesigned and pushed via the API with urgency banners/pills/copy and a conditional subject. Only an optional owner test-send remains. |
| Email logo refresh (all templates) | All nine Brevo templates + their repo source copies swapped from the old `Logo.png` to the new `logo-header-dark.png` lockup (wordmark now in the logo). Pushed via the API. |
| Remember me | Login has a "Remember me" checkbox: ticked = persistent refresh-token cookie (survives browser restart), unticked = session cookie. A companion `rememberMe` cookie preserves the choice across token refresh. Register and reset-password auto-login are session-scoped (no explicit choice made); Google sign-ins are always persistent by default. Plan removed. |
| Branded error pages | Reusable `ErrorDisplay` component drives branded 404 (Router `NotFound`), 403 (authenticated-but-forbidden via `NotAuthorized`) and 500 (error-boundary fallback + `/Error`) states, each with a "Back to dashboard" action. Plan removed. |
| Social sharing (image share) | bet365-style share of a player's own predictions: `GET /api/rounds/{roundId}/share-card` returns a branded PNG (SkiaSharp renderer behind `IShareCardRenderer`, embedded brand logo, team logos fetched server-side with an abbreviation-badge fallback, colour-coded once scored, light/dark theme mirroring the player's UI), surfaced by a Share button on the Active Rounds tile that opens the native share sheet (Web Share API) with a download fallback. Plan trimmed to the remaining per-content Open Graph image residual. |
| Health check endpoints | `/health`, `/health/live` (no checks, liveness only) and `/health/ready` (database + football API), all anonymous. Polled hourly in production by `health-check.yml`, which reports failures to Slack. |
| Correlation IDs | `CorrelationIdMiddleware` stamps every request and log event, and `X-Correlation-Id` is accepted and exposed through CORS. Distributed *tracing* remains open and is constrained by hosting - see `architecture/apm-integration/README.md`. |
| Alerting to Slack | Datadog log monitors for errors and warnings, grouped per environment, into `#alerts-errors` / `#alerts-warnings`; all eight workflows report outcomes to `#github`; production polled hourly for uptime. Plan trimmed to the response-time residual. |
| My Leagues ranks cached on the write path | Rank columns on `LeagueMemberStats` are recomputed by `ILeagueStatsRepository.RefreshLeagueAsync` / `RefreshSeasonAsync` when results change, instead of being derived on every dashboard read. Every rank column is nullable, so a league with no active round reports no rank rather than a fabricated 1st. Migration `0006`; see [ADR-0015](../decisions/0015-cache-my-leagues-ranks.md). |
| Dapper schema check | `tools/ThePredictions.SchemaCheck` verifies every Dapper read in `src/` against the shape SQL Server actually returns, catching result-record drift that the compiler and the mocked handler tests both miss. Runs on demand, or per-commit via the `.githooks/pre-push` hook. |
| Dapper private result records | Every query handler now materialises into a co-located `private record XxxQueryResult(...)` matching its `SELECT` column order and then maps to the outward Contracts DTO by name, so a Contracts reshape is a compile error in the handler instead of a runtime materialisation failure. 33 handlers converted (37 call sites); no Contracts type is a `QueryAsync<T>` generic argument any more. The rule is now in `CLAUDE.md` and `docs/guides/database.md`. Plan removed. |
| Badges in the round-results digest | The digest email celebrates the badges a player earned that round. `EvaluateBadgesForRoundCommand` returns the genuinely-new awards, threaded into the digest as a `BADGES` list; icons are hosted PNGs from a public `GET /api/badges/{key}.png` (badge face rasterised via `Svg.Skia`, glyphs shared in `Contracts.Badges.BadgeGlyphs`, cached). Account/setup badges (mobile, bank, create-league) are now awarded **on-action** via `IBadgeAwardService` (round evaluator remains an idempotent safety net), so they land instantly and stay out of the digest. Plan removed. |
