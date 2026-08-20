# 0018. Log severity says who has to act, not how the request ended

- **Status:** Accepted
- **Date:** 2026-08-20
- **Deciders:** Antony Willson
- **Tags:** technical

## Context

[ADR-0016](./0016-business-rule-exception-classification.md) settled which exception type means "the caller could have satisfied this rule", and mapped that type to **400 and a Warning**. Every other client-fault branch in `ErrorHandlingMiddleware` already logged at Warning too: not found, invalid argument, validation failure, identity update failure, unauthorised, season pass required, email not confirmed.

The consequence showed up in `#alerts-warnings`. The `Web Warnings [{{env.name}}]` monitor fires on **more than zero** `status:warn` events in five minutes and **renotifies every 30 minutes** while unresolved (see [alerting configuration](../todo/architecture/alerting-config/README.md)). A bucket containing every refused request cannot meet that threshold quietly, so the channel alerted more or less continuously.

`EmailNotConfirmedException` was the clearest case. An account that has not confirmed its address trips the same gate on **every** attempt until it clicks the link, so one such player produced warnings indefinitely - all of them saying that a gate had worked correctly, and none of them anything to act on.

The cost is not the noise itself, it is what the noise does to the signal. Warnings that matter - a slow query, a missing index, a third party that has stopped answering - arrived in the same channel as routine refusals and were indistinguishable from them. An alert nobody can read is an alert nobody reads.

Two behaviours in the codebase already had this right, with the reasoning written down. `LoggingBehaviour` logs a failed command at Information, explicitly "so logging it higher would fill the alerts-warnings channel with things that need no action". The middleware and `ValidationBehaviour` did the opposite.

## Decision

We will pick severity by **who has to act**, not by how the request ended.

- **Information** - the caller could have made a different request. Wrong id, failed validation, a rule the current state does not allow, an unconfirmed address, an unauthorised attempt, a pass not held. Recorded in full, so one person's problem can still be investigated; invisible to alerting.
- **Warning** - somebody has to look at this, and it is not the caller's doing. A slow query, a missing index, a third party failing or returning nothing, a data condition an administrator must resolve.
- **Error** - an unhandled or unclassified exception. A defect until proven otherwise, which is the fail-safe default ADR-0016 established and this does not touch.

Applied: all nine client-fault branches in `ErrorHandlingMiddleware` moved from Warning to Information, and `ValidationBehaviour` with them. The status codes are unchanged - a 400 is still a 400 - and so is every message returned to the caller.

ADR-0016 stands. Its decision is about **which type** means a client fault and about `InvalidOperationException` falling through to the unhandled bucket; only the severity half of "400 and a Warning" is revised here.

## Consequences

**For / positive**
- The warnings monitor becomes worth alerting on. What is left in the bucket is what somebody has to act on, so a firing warning is information rather than an interruption.
- The three places that classify a failure - middleware, `ValidationBehaviour`, `LoggingBehaviour` - now agree, where two of three previously disagreed with the one that had documented its reasoning.
- Nothing is lost from the logs. Client faults are still recorded with the same message, and `CorrelationId` is a Datadog facet, so a single request can still be reconstructed.

**Against / cost**
- A genuine defect misfiled as a client fault is now logged at Information rather than Warning, so it is quieter than it was. The mitigating factor is that it was never *alerted* on either - the errors monitor watches `status:error`, which neither level reaches - so this loses no alerting that existed. It does mean a misclassification is harder to notice by eye.
- Anyone reading a client fault at Information has to know that the status code, not the level, says what happened.

**Neutral / notes**
- `ArgumentNullException` is caught on the not-found branch and now logs at Information. It is the one member of the client-fault set with a real claim to being a defect - a null reaching the middleware is more likely our bug than a caller's mistake - and it is worth splitting out later. Left as it was rather than fixed silently inside a severity change.
- `UnauthorizedAccessException` is included, on the grounds that a single refused request is not a security event. If repeated refusals ever need watching, that wants a rate-based rule over the Information records, not a Warning on each one.
- `SeasonPassRequiredException` was the second candidate for the "repeats until acted on" argument and is covered by the same rule.
- The monitor definitions themselves are unchanged and still fire on more than zero warnings. That threshold is only defensible because of this decision.
- `ErrorHandlingMiddlewareTests.InvokeAsync_ShouldLogAtInformation_ForEveryClientFault` pins the whole set in one theory, so a branch added at Warning fails the build rather than surfacing as a page.

## Alternatives considered

- **Raise the monitor threshold** - alert on a spike of warnings rather than any warning. Treats the symptom: the bucket still mixes routine refusals with things needing action, so the mixture is merely quieter, and a real warning still arrives among noise.
- **Exclude specific messages in the Datadog query** - filter out the known-noisy refusals. Puts the classification in the monitor rather than the code, where it is invisible to anyone reading the middleware and has to be updated from outside the repository every time a branch is added.
- **Move only `EmailNotConfirmedException`** - what was originally done. Fixes the loudest source and leaves the category wrong, so the next repeated refusal reopens the problem.
- **A dedicated severity for "client fault"** - neither Serilog nor the sink offers a level between Information and Warning, and inventing one via a property means every monitor has to know about it.

## Related

- [ADR-0016](./0016-business-rule-exception-classification.md) - which exception type means a client fault, and the unclassified-means-server-fault default this keeps.
- [`src/ThePredictions.API/CLAUDE.md`](../../src/ThePredictions.API/CLAUDE.md) - the exception-to-status table, now carrying the severity rule.
- [`docs/guides/logging.md`](../guides/logging.md) - log message formatting.
- [Alerting configuration](../todo/architecture/alerting-config/README.md) - the monitor definitions and thresholds this decision makes defensible.
