# Alerting Configuration

## Status

Not Started | **In Progress** | Complete

Most of this shipped on 2026-07-28. Only response-time alerting remains - see Residual below.

## Summary

Configure alerting rules so problems announce themselves rather than waiting to be noticed.

## Priority

**Medium** - the alerts that matter (errors, warnings, site down) are live. What is left is
performance alerting, which needs a latency signal we do not currently collect.

## Requirements

- [x] Configure error rate alerts - Datadog log monitor on `status:error`
- [ ] Configure response time alerts - **residual**, see below
- [x] Configure availability alerts - hourly GitHub Actions health check, not Datadog
- [x] Configure database connection alerts - the health check fails on an unhealthy database
- [x] Set up notification channels - Slack

## What Shipped

### Datadog log monitors

| Monitor | Query | Grouped by | Channel |
|---------|-------|-----------|---------|
| `Web Errors [{{env.name}}]` | `status:error service:the-predictions-web` | `service`, `env`, `@error.kind` | `#alerts-errors` |
| `Web Warnings [{{env.name}}]` | `status:warn service:the-predictions-web` | `service`, `env` | `#alerts-warnings` |

Both renotify every 30 minutes while unresolved, and evaluate over 5 minutes with missing data
treated as zero.

**Group by `env`, always.** A multi-alert monitor holds a separate alert state per group, so
grouping by environment is what lets a production breach notify while dev is already alerting.
Without it the second environment is silently absorbed.

**Do not group the warnings monitor by `@error.kind`.** Slow-query warnings carry no exception,
so every one of them lands in a single `N/A` group.

### Uptime monitoring

`.github/workflows/health-check.yml` polls production hourly and reports failures to `#github`.
This exists because the Datadog monitors only fire when something **is** logged - a site that
stops serving, a broken log sink or a dead host all look identical to health, and there is no
Datadog Agent on this shared hosting to supply infrastructure metrics. See
[[../apm-integration/README.md]] for why an Agent is not an option.

Liveness uses `/health/live` (no checks, so 200 means the process is serving). Readiness parses
`/health/ready` and treats **only the database** as fatal - the football API is also covered
there, but a third party's outage is not our site being down.

### Workflow outcome notifications

`.github/workflows/notify-slack.yml` is a `workflow_call` target posting to `#github` via an
incoming webhook (`SLACK_WEBHOOK_URL` repository secret). Called from all eight top-level
workflows; deploys, migrations and the dev refresh report both outcomes, CI and the nightly
backup report failures only.

Never call it from `migrate-shared.yml` - that has seven callers and would post a duplicate
every time a deploy ran its migration step.

Slack builds notification previews from the **top-level `text` field**. A payload of only
blocks or attachments shows as "No preview available" on desktop and mobile.

## Residual

**Response-time alerting.** There is no latency signal to alert on today. Request duration is
not recorded as a metric, only inferred from the slow-query warnings, which measure a single
read rather than a request. Options, cheapest first:

1. Generate a log-based metric in Datadog from the durations `LoggingBehaviour` already emits
   for every command, then alert on a percentile. No code change, no ingestion cost.
2. Collect proper request timings, which realistically means the APM work in
   [[../apm-integration/README.md]] - constrained by hosting, so read that first.

Also outstanding: `Datadog:Host` still carries the environment name (`development` /
`production`) rather than machine identity, predating the `env` tag. Worth repointing at a real
hostname once nothing depends on it.

## Technical Notes

The Datadog Serilog sink emits `env:local` / `env:dev` / `env:prod` from `Datadog:Env` per
`appsettings.{Environment}.json`. `env` is a Datadog reserved primary tag, so monitors and
dashboards scope by environment directly.

Service name is `the-predictions-web`, source `csharp`, on the **EU** site
(`app.datadoghq.eu`). The Web host serves the API in-process, so there is one service covering
both; `@Properties.SourceContext` distinguishes the layers.

Slack integration is installed against the `The_Predictions` workspace with `#alerts-errors`
and `#alerts-warnings` registered. With a single workspace connected the short handle form
(`@slack-alerts-errors`) resolves; the account-prefixed form is only needed with several.

`CorrelationId` is a **facet** in Datadog (created 2026-08-03), so an error log can be filtered
down to every other line from the same request. `CorrelationIdMiddleware` stamps it on every request
via `LogContext.PushProperty`, and Serilog's `FromLogContext` enricher carries it to the sink.

Note the correlation id identifies **one HTTP request**, not a user journey: the Blazor client never
sends `X-Correlation-Id`, so the middleware generates a fresh GUID per request and a page load making
five API calls produces five unrelated ids. Making the client generate and propagate one is a small
change to its HTTP handler, deliberately not done - per-request tracing answers "what happened in the
request that failed", which is what the alerting needs.
