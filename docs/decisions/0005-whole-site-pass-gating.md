# 0005. Season Pass gates the whole site, not just money leagues

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** product, legal

## Context

A Season Pass could gate only **money leagues**, or **all participation** (including free leagues) in a pass-required season. Gating only money leagues would tie the fee specifically to the gambling-adjacent functionality.

## Decision

In a pass-required season, a Season Pass is required to take part in **any** league — free or money. The fee is for **access to the whole product**.

## Consequences

**For / positive**
- **Legal cleanliness (primary driver):** the fee is unambiguously "pay for software/access", fully decoupled from money-league/gambling functionality — strengthening the non-gambling position (0002, 0003).

**Against / cost**
- Removes the free on-ramp for casual players in paid seasons (a real growth cost) — mitigated by the first-timer free trial (0006) and by keeping some seasons free (0007).

**Neutral / notes**
- Simplicity and extra revenue are *secondary* benefits, not the reason — the decision was made on legal-cleanliness grounds.

## Alternatives considered

- **Gate only money leagues** — rejected; entangles the subscription revenue with the gambling-adjacent feature, weakening the "it's just software" framing.
- **Free trial period / first free league** — partially adopted via the per-user free trial (0006) rather than an always-free tier within paid seasons.

## Related

- 0002, 0003, 0006, 0007
