# Outstanding Work, Ranked by Decision Effort

A planning aid that ranks the remaining `docs/todo` plans by **how much input the
product owner has to give** before the work can proceed - *not* by engineering
effort. The goal is to make it easy to pick quick wins (little/no decision needed)
versus work that needs a design, business, or legal call first.

> **Snapshot: July 2026** (June snapshot updated after the July architecture
> review added eleven plans). Statuses drift as work ships. A future session
> should re-verify against the codebase before relying on this (the plan files'
> `## Status` lines were themselves stale once - trust the code, not the doc).

## Tier A - Hands-off (no product decisions)

Conventional, technical changes with an unambiguous correct implementation. An
agent can do these autonomously on a branch (keeping the 100% domain coverage and
CI green) and open a PR for review. No input needed until review.

- **jwt-security-hardening** - add explicit `ClockSkew` + a `ValidAlgorithms`
  allow-list to token validation.
- **api-documentation** - Swashbuckle request/response examples + a standard
  `ApiErrorResponse` contract.
- **query-monitoring** - slow-query logging with a sensible threshold (~500ms).
- **third-party-licences** - auto-generated open-source attribution page, linked
  in the footer.

July 2026 review plans, all fully specified with decisions already made:

- **security/server-validation-gap** - revive client validation, add the server
  validation filter (June audit decision; do first).
- **security/refresh-tokens-in-urls** - exchange-code redesign of the Google
  callback; hands-off to build, but merging to production requires the manual
  mobile test checklist on dev to pass.
- **security/origin-header-email-links** - configured base URL instead of the
  Origin header for emailed links.
- **architecture/transaction-context-hardening** - nesting-safe transactions,
  real rollback, post-commit side effects.
- **architecture/composition-root-and-hosting** - Application DI, options
  binding for both hosts, shared pipeline, security headers on the Web host.
- **architecture/error-contract-standardisation** - single ApiErrorResponse
  shape, 401/403 split.
- **architecture/client-service-layer-consolidation** - components onto the
  service layer, silent-failure fixes, atomic league save.
- **architecture/handler-domain-logic-extraction** - scheduling/knockout/state
  machine logic into tested Domain services.
- **architecture/application-infrastructure-leaks** - email template resolver,
  crypto to Infrastructure, provider status normalisation.
- **architecture/dapper-result-records** - private result records in all query
  handlers.
- **architecture/build-tooling** - Directory.Build.props + .editorconfig.

## Tier B - One quick decision, then build

| Plan | The one decision needed |
|------|-------------------------|
| live-score-updates | Poll interval - **decided: 10s** (effectively ready to build) |
| tournament-knockout-result-display | Approve the existing 8-task spec (decisions already made) |
| football-api-resilience (caching) | Acceptable staleness TTLs; user banner yes/no |
| caching-strategy | Which endpoints + TTLs (safe defaults: teams/seasons) |
| pagination | Default page size + response envelope shape |
| error-pages | Tone/copy, or just "match the site" |
| database-migrations (DbUp) | Confirm: adopt DbUp now, baseline current schema |
| per-round-snapshots | Still wanted, now round emails shipped without it? |
| data-export (GDPR) | What goes in the export bundle |
| remember-me | Persistent-login cookie lifetime |
| marketing-opt-in-management | Where the account toggle lives |
| audit-logging / admin-activity-log | Which events to record |
| api-key-rotation | Rotation process (dual-key window?) |
| staging-environment | Separate staging, or is dev enough? |

## Tier C - A short design chat (several UX/product calls)

season-recap - statistics-dashboard - prediction-history - notifications-ui -
prize-summary-badges - monthly-leaderboard-scenarios - digest-emails -
email-preferences - league-notifications - head-to-head - achievements-badges -
social-sharing - help-documentation - accessibility - admin-dashboard

## Tier D - Big builds / business, legal, or external-account decisions

season-passes Phase B (Stripe account, products, refunds, legal) - comms-strategy
+ WhatsApp (Meta/BSP onboarding, opt-in, cost) - season-challenges -
two-factor-auth - account-recovery - multi-device-sessions - league-chat -
content-moderation - report-management - support-tools - system-announcements -
bulk-operations - pwa-support - offline-support - search-functionality -
public-profiles - mini-leagues (sub-league product design) - read-replicas -
cdn-static-assets - apm-integration - alerting-config - dead-letter-queue -
data-archiving - admin-ip-protection - penetration-testing -
suspicious-activity-detection - code-consistency-audit

## No build needed - already decided (accepted risks)

These are deferred **by decision**, documented in `docs/security/accepted-risks.md`.
They only need work if the decision is reversed.

- localstorage-tokens

(refresh-tokens-in-urls and server-validation-gap were un-deferred by the
June/July 2026 reviews and moved to Tier A above.)
