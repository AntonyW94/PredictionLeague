# Feature: Round Results Digest Email

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #105). `SendRoundDigestEmailsCommand`/`Handler`, `GetRoundDigestQuery`, `DigestEmailFormatter`, Brevo template 11, per-league results links, and admin resend are all live. Idempotent via `Round.ResultsDigestSentUtc`.

## Priority

**Phase 2 - High-impact** (per [comms strategy](../../comms-strategy/README.md))

## Summary

After a round's results are processed, send each user who participated an email summarising how they did - points earned, predictions correct/wrong, league position change, and a link to view full results. One email per user per round (across all their leagues, like the existing reminder pattern), sent the morning after the round closes so it lands when people are checking their phones over breakfast.

## User Story

As a player, I want a digestible summary of how my predictions went after each round, so I can see my score, compare to other players, and feel the satisfying drip of progress through the season - without having to log in.

## What triggers it

The point at which all matches in a round have results and scoring is finished. In the codebase this is when [`UpdateMatchResultsCommandHandler`](../../../../src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateMatchResultsCommandHandler.cs) marks a round as complete, which is followed by [`ProcessPrizesCommandHandler`](../../../../src/ThePredictions.Application/Features/Admin/Rounds/Commands/ProcessPrizesCommandHandler.cs) being invoked.

The cleanest place to hook in is a new `SendRoundDigestEmailsCommand` that runs after `ProcessPrizes` and before any prize notifications. That keeps the order: score the round, decide prizes, tell users about results (digest), then tell winners they won (prize notification).

Send-time: don't fire instantly. Schedule for ~08:00 the morning after round completion so the email lands at a reasonable hour. Worst case the existing scheduler does this.

## Design / Mockup

```
+----------------------------------------------------+
|  [logo]  The Predictions                           |
+----------------------------------------------------+
|                                                    |
|  Hi Antony,                                        |
|                                                    |
|  Round 12 is in the books. Here's how you did:     |
|                                                    |
|  +----------------------------------------------+  |
|  |  YOUR SCORE                          18 pts  |  |
|  |  6 correct results, 2 exact scores           |  |
|  +----------------------------------------------+  |
|                                                    |
|  League positions:                                 |
|  - Office League:    3rd  (^ up 1)                 |
|  - Family Cup:       1st  (-- no change)           |
|                                                    |
|  Top scorer this round: Sarah J. (24 pts)          |
|                                                    |
|  [ View full round results ]                       |
|                                                    |
|  Next round opens at: Sat 18:00                    |
|  Deadline: Sat 14:30                               |
|                                                    |
+----------------------------------------------------+
|  Manage email preferences | (c) The Predictions    |
+----------------------------------------------------+
```

## Acceptance Criteria

- [ ] Each user who submitted at least one prediction in the round receives exactly one email.
- [ ] Users in multiple leagues see all their league positions in one email, not one email per league.
- [ ] Score is correct and matches the leaderboard at the time of send.
- [ ] Position change indicator (`up`, `down`, `--`) reflects the move from previous round to this one.
- [ ] Top scorer of the round is named (per league context where useful, otherwise the global top scorer across the league(s) the user is in).
- [ ] Next-round info (open time + deadline) is included if a next round exists.
- [ ] Built on the [email design system](../email-templates/README.md) - same header, footer, type, button styling.
- [ ] Plain text version reads well.
- [ ] Email is not sent to users who haven't predicted (they get the "predictions missing" reminder instead - separate flow, separate template).
- [ ] Test sends from the [admin email test tool](../../admin-moderation/email-test-tool/README.md) work end-to-end.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Brevo template | Author `Round Results Digest` template on the design-system base. Add to `BrevoSettings.Templates.RoundResultsDigest`. | Not Started |
| 2 | Aggregation query | New query handler `GetRoundDigestDataForUserQuery` returning everything one user needs for their digest: round name, points, correct counts, league rows, next-round info. SQL via `IApplicationReadDbConnection` per CQRS rules. | Not Started |
| 3 | Send command | `SendRoundDigestEmailsCommand` that loads all participating users for the completed round, calls the aggregation query per user, sends the email. Idempotent - tracks sent state on the round so re-running doesn't double-send. | Not Started |
| 4 | Wire into round-completion flow | Invoke the new command after `ProcessPrizesCommand` completes successfully. | Not Started |
| 5 | Round entity flag | Add `ResultsDigestSentUtc` (nullable datetime) to `Round` so we can prevent re-sends. Schema change - update `database-schema.md` and `DatabaseRefresher.cs` per project rules. | Not Started |
| 6 | Tests | Unit tests for the aggregation query (correct counts, position deltas) and the send command (idempotency, only-participants filter, content shape). | Not Started |

## Dependencies

- [ ] [Email design system](../email-templates/README.md) shipped first (otherwise this email won't match the rest).
- [ ] [Admin email test tool](../../admin-moderation/email-test-tool/README.md) is a strong recommendation - this is a complex template with lots of params, and iterating without a test tool is painful.

## Technical Notes

### Cost

Email-only. ~50 users per round = 50 sends per round = ~150-200/month at one round per week. Well under Brevo's 300/day free tier.

### Why one email across leagues, not one per league

Users in 2-3 leagues would otherwise get 2-3 emails for one round - feels spammy and they'd unsubscribe. Aggregating gives a "your week in The Predictions" feel which is much nicer. The trade-off is a slightly more complex template (loop over leagues) - acceptable.

### Top scorer scope

Showing "top scorer this round" is high-engagement content (people love comparing themselves to others). For a user in multiple leagues this is ambiguous - whose round are we showing? Recommend: show top scorer per league inline next to each league's position, not as a separate "top scorer" line. Single league = one row, multi-league = multiple rows. Cleaner.

### Don't send to non-predictors

Users who didn't predict the round get a "predictions missing" chase before deadline (already exists). After the round, they get nothing - sending them a results email saying "you scored 0" is depressing and counter-productive for retention.

### Idempotency

The send command must check `ResultsDigestSentUtc` and skip if already set, then update on success. This protects against accidentally re-running the post-round flow during admin actions or after a deploy mid-flow.

## Open Questions

- [ ] Show the user their predictions (e.g. "you predicted Liverpool 2-1, actual was 2-0") in the email, or keep it summary-only and link to the site for detail? Detail is nicer but doubles email length and template complexity. Lean: summary only, link to site.
- [ ] Time zone for the "morning after" send. Currently no per-user time zone capture. Send at a UK-friendly time (08:00 UTC ~= 08:00 GMT) and accept that overseas users get it whenever; revisit if/when international users become a thing.
- [ ] Use Brevo's batch send API for efficiency, or send one-at-a-time like the existing reminders? Batch saves API calls but per-user param data makes it awkward. Lean: one-at-a-time, matches existing pattern.
