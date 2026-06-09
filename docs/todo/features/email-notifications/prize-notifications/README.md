# Feature: Prize Won Notification

## Status

Not Started | In Progress | **Complete** *(email v1; WhatsApp phase deferred)*

> **Verified June 2026:** Email v1 shipped (PR #106). `SendPrizeNotificationsCommand`/`Handler`, `PrizeNotification` sent-log (idempotency), `GetPrizeWinnersForRoundQuery`, `PrizeNotificationFormatter`, Brevo template 12, and admin "Resend prizes" are all live. The WhatsApp channel (original task 7) remains a deferred follow-up.

### Implementation notes (v1, differs from original plan)

- **Idempotency via a `[PrizeNotifications]` sent-log, not a persisted `PrizeAwards` table.** `Winnings`
  already are the award record, but each `IPrizeStrategy` deletes and re-creates them every
  re-process, so they can't carry a "notified" flag. The append-only log is keyed on the winning's
  stable identity `(UserId, LeaguePrizeSettingId, RoundNumber, Month)` and is the dedup source.
- **One grouped email per winner** covering all their prizes for the round (Brevo `{% for %}` loop),
  rather than one email per prize as the original AC suggested.
- **Email shows prize label and monetary value.** Winners only - no runner-up notifications in v1.
- Sent from `SendPrizeNotificationsCommand`, wired into `UpdateMatchResultsCommandHandler` after the
  results digest. Admin force-resend: `POST /api/admin/rounds/{id}/resend-prize-emails` + button.
- Brevo template "Prize Won" is id `12`; HTML at `docs/email-templates/prize-won.html`.
- WhatsApp portion (task 7) remains a Phase 4 follow-up.

## Priority

**Phase 2 - High-impact** (per [comms strategy](../../comms-strategy/README.md))

## Summary

When `ProcessPrizesCommandHandler` awards a prize to a user, that user gets a celebratory email telling them they've won. Once WhatsApp infrastructure exists (Phase 4), opted-in winners also get a WhatsApp message - the dopamine-hit channel for what is the most exciting moment in the product.

This is a low-volume, high-impact notification. A handful of prizes per round per league - cost is trivial, but the moment is worth doing well.

## User Story

As a player, when I win a prize - whether for the round, monthly leaderboard, or end-of-season - I want to be told straight away with something that feels like a celebration, so I share the moment with friends and stay engaged with the league.

## What triggers it

[`ProcessPrizesCommandHandler`](../../../../src/ThePredictions.Application/Features/Admin/Rounds/Commands/ProcessPrizesCommandHandler.cs) iterates over a league's `PrizeSettings` and runs the matching `IPrizeStrategy` for each. Each strategy decides who has won what.

The cleanest hook is to have each strategy raise a `PrizeAwarded` event (or return a list of awards), then a single `SendPrizeNotificationsCommand` consumes the awards and dispatches notifications. This keeps strategies focused on prize logic and notification logic in one place.

Send-time: shortly after `ProcessPrizes` completes - prize notifications go out *after* the round results digest, so the order users experience is: "here's how you did" then (for winners) "and you won X!".

## Design / Mockup

### Email

```
+----------------------------------------------------+
|  [logo]  The Predictions                           |
+----------------------------------------------------+
|                                                    |
|     [trophy icon]                                  |
|                                                    |
|     You won!                                       |
|                                                    |
|  Hi Antony,                                        |
|                                                    |
|  Congratulations - you've won the                  |
|  *Round 12 Top Scorer* prize in                    |
|  the Office League.                                |
|                                                    |
|  Prize: GBP 10 Amazon voucher                      |
|                                                    |
|  Final score: 24 points                            |
|  Margin over 2nd place: 3 points                   |
|                                                    |
|  [ View league leaderboard ]                       |
|                                                    |
|  Your league admin will be in touch about          |
|  collecting your prize.                            |
|                                                    |
+----------------------------------------------------+
|  Manage email preferences | (c) The Predictions    |
+----------------------------------------------------+
```

### WhatsApp (utility template, Phase 4 onwards)

```
You won! [trophy emoji]

Round 12 Top Scorer in Office League.
Prize: GBP 10 Amazon voucher.
Score: 24 pts (3 ahead of 2nd).

View leaderboard: {{ leaderboardLink }}

Your league admin will be in touch.
```

## Acceptance Criteria

- [ ] When a user wins any prize, they receive exactly one email per prize (multiple prizes in one round = multiple emails - acceptable for v1, can batch later if it becomes annoying).
- [ ] Email mentions the specific prize name, the league, and the prize value/description as configured.
- [ ] Email reuses the design system header/footer.
- [ ] Email handles all prize types: round-level (top scorer of round), period-level (monthly leaderboard), season-level (overall winner, runner-up, etc.).
- [ ] Plain text version reads well.
- [ ] Once Phase 4 lands: opted-in winners with a phone number also receive a WhatsApp message. Email still goes regardless.
- [ ] Idempotent - re-running prize processing doesn't double-notify.
- [ ] Test sends work via the [admin email test tool](../../admin-moderation/email-test-tool/README.md).

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Brevo template | Author `Prize Won` template on the design-system base. Celebratory tone, trophy/medal visual. Add to `BrevoSettings.Templates.PrizeWon`. | Not Started |
| 2 | Domain - prize award model | Decide how prize awards are represented. Options: a new `PrizeAward` entity persisted, or a transient list returned from each strategy. Persisted is preferable - gives a permanent record and supports idempotency. New table `[PrizeAwards]`. | Not Started |
| 3 | Refactor strategies to record awards | Each `IPrizeStrategy.AwardPrizes` writes one or more `PrizeAward` rows instead of (or in addition to) whatever it does today. | Not Started |
| 4 | Send command | `SendPrizeNotificationsCommand` loads `PrizeAwards` not yet notified, sends emails, marks notified. | Not Started |
| 5 | Wire into round-completion flow | Invoke after `ProcessPrizesCommand` and after `SendRoundDigestEmailsCommand` so users get results first, prize email second. | Not Started |
| 6 | Schema | Add `[PrizeAwards]` table. Update `database-schema.md` and `DatabaseRefresher.cs` per project rules. Anonymise as needed in `DataAnonymiser.cs`. | Not Started |
| 7 | Phase 4 follow-up | Once WhatsApp infra lands, extend `SendPrizeNotificationsCommand` to also dispatch WhatsApp to opted-in users. Don't block this feature on Phase 4 - email-only is the v1. | Not Started |
| 8 | Tests | Unit tests for prize-award persistence and send command (idempotency, content correctness, opt-in respect when WhatsApp added). | Not Started |

## Dependencies

- [ ] [Email design system](../email-templates/README.md) - shared visual language.
- [ ] [Round results digest](../round-results-emails/README.md) - sends in the same flow, runs first. Not technically blocking but the order matters for UX.
- [ ] WhatsApp integration (Phase 4) - blocking only for the WhatsApp portion. Email-only v1 doesn't need it.

## Technical Notes

### Why persist PrizeAwards rather than send inline

Today `ProcessPrizesCommandHandler` is fire-and-forget per strategy. If the email send is wired into the strategy, a Brevo outage stops prize processing, and there's no record of what was awarded if we want to re-send or re-render. Persisting `PrizeAwards` decouples awarding from notifying:

- Award is the source of truth ("who won what when").
- Notification is a separate concern that reads from the table and tracks send state.
- Re-running processing can detect existing awards and skip.
- Future features (a "prizes won" page on the user's profile) read from the same table.

### Cost

Volume estimate at 50 users, 5 leagues, average 1-2 prizes awarded per round across all leagues = ~5 emails/week = ~20/month. Negligible. WhatsApp portion at ~5 sends/month = ~£0.20.

### Multiple prizes in one round

A user could win round-top-scorer AND a fortnight prize in the same processing run. v1 sends one email per prize - feels celebratory if anything. If it becomes annoying we can batch into one email later (`PrizeNotificationBatch`).

### Avoiding "you won" before the user knows the round is over

Currently no rule says digest must precede prize email - they're sent close together but not strictly ordered. Make the prize-email step explicitly run after the digest step in the flow so winners always see "here's the round" then "and you won".

## Open Questions

- [ ] Visual: actual trophy/medal image inline vs. emoji vs. design-token coloured banner? Image is nicest but Brevo asset hosting required. Lean: coloured banner + crown icon (lightweight).
- [ ] Mention the prize *value* or just the prize *name*? League admins set both (e.g. "Round Top Scorer" / "GBP 10 Amazon voucher"). Show both - users care.
- [ ] Should losing-runner-up users get any notification? "You came 2nd, well played" type. Probably not v1 - it's nice in theory but starts to feel like noise at scale.
- [ ] Multi-prize batching - do later if needed, not v1.
