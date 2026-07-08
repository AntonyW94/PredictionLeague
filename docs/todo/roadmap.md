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
| 3 | Database migrations (DbUp) | Medium | Manual schema changes across 3 databases is unsustainable | [Outline](architecture/database-migrations/README.md) |
| 4 | Request timeout configuration | Low | No explicit timeouts currently = silent hangs in production | [Outline](architecture/request-timeouts/README.md) |
| 6 | Health check endpoints | Low | Small implementation, enables monitoring. Needed for alerting and E2E workflow | [Outline](architecture/health-checks/README.md) |
| 7 | Football API resilience | High | Site completely fails if the API goes down. Circuit breaker + caching essential | [Outline](architecture/football-api-resilience/README.md) |
| 8 | Database resilience | Medium | Connection pooling + retry policies for shared hosting | [Outline](architecture/database-resilience/README.md) |
| 9 | Alerting configuration | Medium | Datadog already integrated; add monitors so you know when things break | [Outline](architecture/alerting-configuration/README.md) |
| 10 | Distributed tracing / correlation IDs | Medium | Serilog request logging exists but no correlation ID middleware or headers yet | [Outline](architecture/distributed-tracing/README.md) |
| 11 | Caching strategy | High | Every request currently hits the database. Leaderboards/teams/seasons are obvious cache targets | [Outline](architecture/caching-strategy/README.md) |
| 12 | Pagination | Medium | Needed once you have real data volumes in leagues and leaderboards | [Outline](architecture/pagination/README.md) |
| 13 | Query handler integration tests (Phase 3) | Medium | Catches SQL mapping bugs. Highest-value test tier after domain tests | [Detailed plan](architecture/test-suite/README.md) |
| 14 | Command handler unit tests (Phase 5) | Medium | Test business logic orchestration with mocked repositories | [Detailed plan](architecture/test-suite/README.md) |
| 15 | Code consistency audit | Medium | Clean up tech debt now that patterns are established | [Outline](architecture/code-consistency-audit/README.md) |
| 16 | Query performance monitoring | Medium | Find slow queries before users feel them | [Outline](architecture/query-monitoring/README.md) |
| 17 | E2E tests with Playwright (Phase 7) | High | Confidence for critical user journeys | [Detailed plan](architecture/test-suite/README.md) |

---

## Track 2: Quick Wins

*Legal requirements, auth gaps, security, and polish. Low-to-medium effort items that need doing.*

| # | Item | Effort | Why | Plan |
|---|------|--------|-----|------|
| 18 | Copyright footer | Trivial | Needed before any public users see the site | [Outline](features/legal-compliance/copyright-footer/README.md) |
| 19 | Remember me | Low | Users expect persistent login | [Outline](features/authentication/remember-me/README.md) |
| 20 | Error pages (404, 500) | Low | Basic error page exists but no specific 404/500 pages | [Outline](features/user-experience/error-pages/README.md) |
| 21 | Privacy & terms pages | Low | Legal requirement for any site collecting user data | [Outline](features/legal-compliance/privacy-terms-pages/README.md) |
| 22 | Signup legal checkboxes | Low | Legal requirement; depends on #21 existing first | [Outline](features/legal-compliance/signup-legal-checkboxes/README.md) |
| 23 | Email verification | Medium | DB column exists but no verification email or token flow yet. Must know emails are real | [Outline](features/authentication/email-verification/README.md) |
| 24 | Cookie consent | Medium | GDPR mandatory before public launch in the UK | [Outline](features/legal-compliance/cookie-consent/README.md) |
| 25 | Audit logging | Medium | Track who did what, for security and debugging | [Outline](security/audit-logging/README.md) |
| 26 | Data export (GDPR) | Medium | GDPR right to data portability | [Outline](features/legal-compliance/data-export/README.md) |
| 27 | Deferred security items | Medium | JWT hardening, open redirect fix. Clean up once login system is stable | [Outline](security/) |

---

## Track 3: New Features

*User-facing functionality that drives engagement and retention.*

| # | Item | Effort | Why | Plan |
|---|------|--------|-----|------|
| 30 | User onboarding | Medium | Reduce drop-off for new signups | [Outline](features/user-experience/user-onboarding/README.md) |
| 31 | Email preferences | Medium | Let users control what they receive | [Outline](features/email-notifications/email-preferences/README.md) |
| 32 | Notifications UI | Medium | Dashboard alerts tile exists; extend to bell icon and dropdown for general notifications | [Outline](features/user-experience/notifications-ui/README.md) |
| 33 | Accessibility (WCAG basics) | Medium | Right thing to do, also a legal consideration | [Outline](features/user-experience/accessibility/README.md) |
| 34 | Achievement badges | Medium | Gamification to increase retention | [Outline](features/user-experience/achievements-badges/README.md) |
| 35 | Prediction history | Medium | Data exists in DB and is shown in league dashboards; needs a dedicated history view | [Outline](features/user-experience/prediction-history/README.md) |
| 36 | League notifications | Medium | Join request notifications exist; extend to broader league events | [Outline](features/email-notifications/league-notifications/README.md) |
| 37 | Admin dashboard | Medium | Admin CRUD pages exist; need a summary/overview page with stats | [Outline](features/admin-moderation/admin-dashboard/README.md) |
| 38 | Statistics dashboard | Medium | Some stats exist across league dashboards; needs a unified personal stats page | [Outline](features/user-experience/statistics-dashboard/README.md) |
| 39 | Season recap | Medium | End-of-season summary. Shareable, fun | [Outline](features/user-experience/season-recap/README.md) |
| 40 | Head-to-head comparison | Medium | Social/competitive feature | [Outline](features/user-experience/head-to-head/README.md) |
| 41 | Social sharing | Low | Organic growth driver | [Outline](features/user-experience/social-sharing/README.md) |
| 42 | Digest emails | Medium | Weekly summaries for less active users | [Outline](features/email-notifications/digest-emails/README.md) |
| 43 | League moderation | Medium | Basic member management exists; extend to full moderation tools | [Outline](features/admin-moderation/league-moderation/README.md) |
| 44 | Live score auto-update (no refresh) | Low | Scores update in the DB every minute but the WASM client doesn't auto-refresh; add 10s client polling on live pages (SignalR is the later upgrade) | [Outline](features/user-experience/live-score-updates/README.md) |

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
- Third-party licences

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
