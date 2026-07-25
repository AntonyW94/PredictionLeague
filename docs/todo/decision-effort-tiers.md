# Outstanding Work, Ranked by Decision Effort

A planning aid that ranks the remaining `docs/todo` plans by **how much input the
product owner has to give** before the work can proceed - *not* by engineering
effort. The goal is to make it easy to pick quick wins (little/no decision needed)
versus work that needs a design, business, or legal call first.

> **Snapshot: re-verified against the codebase 2026-07-25.** Every plan's
> `## Status` line was checked against the actual code on this date. Items that
> have since shipped are listed under "Shipped" below and removed from the tiers.
> Statuses still drift as work ships, so a future session should re-verify against
> the code before relying on this - trust the code, not the doc.

## Shipped - verified in code (2026-07-25)

No longer outstanding; removed from the tiers below. Each plan's own `## Status`
line already reflects this.

- **live-score-updates** - Complete (pragmatic version): client-side, visibility-aware
  10s polling on the league and user dashboards. Cache-backed reads and the
  stale-data banner are consciously deferred to the caching / resilience work.
- **season-passes** - Phase A + launch Complete and **live on prod** (Standard tier,
  Stripe Checkout, webhook fulfilment, trial/free-acquire, running-costs calculator,
  email-verification gate). Remaining: merge the feature branch into `master`;
  SMS/Premium tier and self-service refunds are deliberately deferred (see its README).
- **database-migrations** - Complete: DbUp migrator + numbered embedded scripts +
  migrate workflows (ADR-0013).
- **achievements-badges** - Complete: badge catalogue, awarding engine (hooked into
  match-result updates), backfill command, and display.
- **incomplete-predictions-visibility** - Done: round-completion query, admin
  completion page, dashboard tile, reminder wiring.

## Tier A - Hands-off (no product decisions)

Conventional, technical changes with an unambiguous correct implementation. An
agent can do these autonomously on a branch (keeping the 100% domain coverage and
CI green) and open a PR for review. No input needed until review.

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
- **architecture/build-tooling** - `Directory.Build.props` + `.editorconfig` shipped (2026-07-25); only the `AnalysisLevel` raise to `latest-recommended` remains (a cascading, multi-project code-change effort - see the plan).

## Tier B - One quick decision, then build

| Plan | The one decision needed |
|------|-------------------------|
| football-api-resilience (caching) | Acceptable staleness TTLs; user banner yes/no. **Partial:** retry/health-check resilience already shipped; only the read-through cache + degraded banner remain. |
| caching-strategy | Which endpoints + TTLs (safe defaults: teams/seasons) |
| pagination | Default page size + response envelope shape |
| error-pages | Tone/copy, or just "match the site" |
| per-round-snapshots | Still wanted, now round emails shipped without it? |
| data-export (GDPR) | What goes in the export bundle |
| remember-me | Persistent-login cookie lifetime |
| marketing-opt-in-management | Where the account toggle lives. **Partial:** `MarketingOptInAtUtc` + registration consent exist; the account-page toggle + wiring are not built. |
| audit-logging / admin-activity-log | Which events to record |
| api-key-rotation | Rotation process (dual-key window?) |
| staging-environment | Separate staging, or is dev enough? |

## Tier C - A short design chat (several UX/product calls)

season-recap *(partial - recap tile shipped; standalone/shareable surface not built)* -
statistics-dashboard - prediction-history - notifications-ui -
prize-summary-badges - monthly-leaderboard-scenarios - digest-emails *(the periodic/weekly digest; the round-results digest already ships)* -
email-preferences - league-notifications *(partial - join-request/approval emails ship; broader league-event notifications + opt-out remain)* -
head-to-head - social-sharing *(partial - baseline OG/Twitter tags ship; share buttons remain)* -
help-documentation - accessibility - admin-dashboard

## Tier D - Big builds / business, legal, or external-account decisions

season-passes SMS/Premium tier *(the paid Standard launch already shipped to prod; only the deliberately-deferred SMS/Premium tier and self-service refunds remain, plus the branch merge)* -
comms-strategy + WhatsApp (Meta/BSP onboarding, opt-in, cost) *(partial - email design system + results/prize emails ship; WhatsApp + notification preferences remain)* -
season-challenges *(the badges engine is already built via achievements-badges; only paid-pass gating remains)* -
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
