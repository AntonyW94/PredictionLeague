# Alerting Configuration

## Status

**Not Started** | In Progress | Complete

## Summary

Configure alerting rules in Datadog to notify of issues proactively.

## Priority

**High** - Datadog integrated but no alert rules defined

## Requirements

- [ ] Configure error rate alerts
- [ ] Configure response time alerts
- [ ] Configure availability alerts
- [ ] Configure database connection alerts
- [ ] Set up notification channels (email, Slack, etc.)

## Alert Types to Configure

| Alert | Threshold | Notification |
|-------|-----------|--------------|
| Error rate | > 5% over 5 minutes | Email |
| Response time | > 2s p95 | Email |
| Availability | < 99% over 5 minutes | Email + SMS |
| Database errors | Any | Email |

## Technical Notes

Datadog is already integrated for logging. Need to configure:
- Monitors
- Alert conditions
- Notification routing

### Log tagging (done)

The Datadog Serilog sink emits `env:local` / `env:dev` / `env:prod` (from `Datadog:Env` per
`appsettings.{Environment}.json`). `env` is a Datadog reserved primary tag, so monitors and
dashboards can scope by environment directly.

Group log monitors by `env` so each environment holds its own alert state - otherwise the
monitor is already in ALERT for one environment and a later breach in another is silently
absorbed instead of notifying.

Note `Datadog:Host` is also set per environment (`development` / `production`) and predates the
`env` tag, so it currently carries environment rather than machine identity. Worth repointing at
a real hostname once nothing depends on it.

### Slack notification channels

The Slack integration is installed against the `The_Predictions` workspace, with
`#alerts-errors` and `#alerts-warnings` registered for monitor alerts. Handles take the form
`@slack-The_Predictions-<channel>`.
