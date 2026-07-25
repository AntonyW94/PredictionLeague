# Feature: Badges In The Round-Results Digest Email

## Status

**Not Started** | In Progress | Complete

## Summary

Show the badges a player earned this round in the round-results digest email ("You earned Sharpshooter this round!"). The badge feature already evaluates and awards badges at round completion; this surfaces the newly earned ones in the email that goes out moments later, to pull players back in.

## User Story

As a player, I want the results email to tell me which badges I just earned, so that I feel rewarded and come back to see my trophy cabinet.

## Context / why this is cheap

Badge evaluation is deliberately hooked into `UpdateMatchResultsCommandHandler` **after** prizes and **before** `SendRoundDigestEmailsCommand`, so by the time the digest is built the new awards already exist. The only missing piece is passing the newly awarded badges through to the email and rendering them. The badges feature itself is already shipped in code (`BadgeCatalogue`, `EvaluateBadgesForRoundCommandHandler`); its plan has been completed and removed.

## Acceptance Criteria

- [ ] A player who earned one or more badges in the round sees them listed in their digest email (badge name, ideally the glyph/icon).
- [ ] A player who earned nothing sees no badges section (not an empty block).
- [ ] Only **live** round completions email badges - the one-off backfill stays silent (it never sends email).
- [ ] Idempotent: re-completing a round does not re-send (already guarded by `Round.ResultsDigestSentUtc`).
- [ ] Repeatable badges earned this round (e.g. Round Winner, Beat the Crowd) are included; a lifetime badge only appears in the round it was actually earned.

## Approach

1. **Return the new awards.** `EvaluateBadgesForRoundCommand` currently returns nothing; `IUserBadgeRepository.AwardAsync` already returns `true` only on a genuinely new insert. Collect the `(UserId, BadgeKey)` awards that were newly inserted for this round and return them from the command (or expose a `GetBadgesAwardedForRoundAsync(roundId)` read for the digest to call).
2. **Thread into the digest.** In `SendRoundDigestEmailsCommand`/handler, for each recipient look up the badges they earned this round and map keys -> display names/glyphs via `BadgeCatalogue`.
3. **Template.** Add a badges block to the round-results digest Brevo template (managed via the API - see [`docs/processes/brevo-template-management.md`](../../../../processes/brevo-template-management.md)). Use a repeatable merge structure for the list; hide the block when empty.
4. **Only live sends.** The backfill path (`BackfillBadgesCommand`) must never trigger the digest - it already only awards, so keep it that way.

## Files likely touched

- `EvaluateBadgesForRoundCommandHandler` (surface new awards) / a small read query for "badges awarded in round N".
- `SendRoundDigestEmailsCommand` + handler.
- The round-results digest Brevo template.
- `BadgeCatalogue` (name/glyph lookup by key - already available).

## Dependencies

- [x] Achievements & Badges feature (shipped).
- [ ] Brevo round-results digest template edit.
