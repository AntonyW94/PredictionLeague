# APM Integration

## Status

**Not Started** | In Progress | Complete

## Summary

Implement full Application Performance Monitoring beyond current logging.

## Priority

**Low** - Post-launch improvement (Datadog logging already integrated)

## Blocker: Datadog APM Is Not Possible On This Hosting

**Established 2026-07-28. Read this before evaluating anything else.**

The site runs on shared hosting with no server access at all - no RDP, no shell. Deploys are a
local folder publish that GitHub Actions uploads by `lftp`, and there is no MSDeploy target, so
this is not Azure App Service either.

That rules out Datadog APM outright:

- The .NET tracer delivers traces to a Datadog **Agent on localhost**. There is no supported
  agentless mode that posts traces straight to Datadog's intake.
- The one route that avoids OS access is the Azure App Service site extension, which needs App
  Service. Not applicable.
- There will also never be infrastructure telemetry from Datadog here - no CPU, memory, disk or
  process monitoring. Observability on this hosting is permanently application-level.

The Datadog bill is currently about $0.10/month precisely because there are **no monitored
hosts**; logs arrive over HTTPS from the Serilog sink. Installing an Agent would introduce
per-host Pro charges and change the economics completely.

This decision is worth an ADR if it is ever revisited.

## Requirements

- [x] Evaluate full Datadog APM vs current logging-only - **ruled out, see Blocker above**
- [ ] Choose an agentless APM, or decide the log-based approach is enough
- [ ] Configure distributed tracing
- [ ] Configure performance baselines

Service maps and anomaly detection were dropped: both are Datadog APM features, so they go with
the blocker above rather than being separate tasks.

## Agentless Options

All pure in-process SDKs over HTTPS, so all viable on shared hosting:

- **Application Insights** - strongest fit. Gives dependency tracking including SQL call
  durations, which is the visibility the slow-query Warning logs only approximate. Does not
  require hosting in Azure.
- **Sentry** - performance tracing alongside error tracking.
- **Raygun** - also agentless.

## Cheapest First Step, No New Vendor

`CorrelationIdMiddleware` already stamps every log event, and since 2026-07-28 our own
namespaces log at Information with `LoggingBehaviour` recording the duration of every command.
Filtering Datadog by correlation id therefore already reconstructs a request's sequence, and a
log-based metric over those durations would give percentile alerting without an APM vendor.

Try that before taking one on - it may well be enough, and it is what
[[../alerting-config/README.md]] needs for its outstanding response-time alerting.

## Current State

- Datadog integrated for logging only, with error and warning monitors live
- Uptime monitoring via a scheduled GitHub Actions health check, not Datadog
- No distributed tracing, no request-level performance metrics

## Technical Notes

Full Datadog APM would provide:
- Distributed tracing across services
- Service dependency maps
- Performance percentiles
- Error tracking with stack traces
- Database query analysis

Cost consideration: Full APM is significantly more expensive than logging-only.
