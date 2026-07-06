# Feature: Incomplete-Predictions Visibility + Ad-hoc Reminders

## Status

Not Started | **In Progress** | Complete

> Implemented on branch `incomplete-predictions-visibility` (all 8 tasks). Pending: PR review, and
> applying migration `0003` (additive - safe ahead of the code deploy).

## Priority

**Medium**

## Summary

Give admins and league owners a clear view of who has only *partially* completed their
predictions for a round, and let them send an ad-hoc reminder email to those players. A
"partial" entry is the dangerous state - the player thinks they are done - so the feature
exists mainly to surface and act on it.

## Motivating incident (Round 5, World Cup 2026)

During the Round of 16 (`Rounds.Id = 43`, season `World Cup 2026`, `SeasonId = 2`), three
players entered only 6 of the 8 fixtures:

- `dave1st@gmail.com`
- `frankmacklin98@outlook.com`
- `richard.westerbeek@gmail.com`

All three missed **exactly the same two matches** - match 95 (Argentina v Egypt) and match 96
(Switzerland v Colombia), the two chronologically latest kick-offs. The cause was **not**
forgetfulness: those two fixtures were still placeholders (teams not yet confirmed) at the
time the players predicted, so the site did not present them. The players saw that they had
predicted "the matches that were there" and assumed they were complete, not realising more
fixtures would be confirmed later in the window.

The site is arguably clear enough about this, but there is currently **no way for an admin or
league owner to see who is short of predictions and nudge them** before the deadline. This
feature closes that gap.

> One-off resolution for Richard: he sent his intended scores before the round's first match
> kicked off, so his two missing predictions (Argentina 3-0 Egypt = 3-0; Colombia to beat
> Switzerland 2-0 = home 0, away 2) were inserted manually, backdated to the same
> `CreatedAtUtc` (`2026-07-04 06:57:20`) as his other six, after taking a prod backup. This
> was a courtesy exception, not a precedent.

## Key architectural fact

**Predictions are stored per season, not per league.** `UserPredictions` is keyed on
`UserId` + `MatchId` only - there is no `LeagueId`. A player makes **one** set of predictions
per round, and those same predictions count in every league they belong to. Two consequences
drive the whole design:

1. "Completion" is fundamentally a **round** fact. A league is only a *lens* (a way to filter
   to the people an owner cares about).
2. A reminder is **per user, not per league**. If a player is in three leagues and each owner
   nudges them, they must not receive the same reminder three times. Dedup happens at
   `(RoundId, UserId)` level.

## User story

As a **site admin or league owner**, I want to see which players are missing predictions for a
round (and which specific fixtures), so that I can send them a reminder before the deadline.

## Design / placement

Two entry points, one shared completion query + one shared reminder command:

```
Site admin ─────►  /admin/rounds/{roundId}/completion
                   All participants in the season for this round (distinct users)

League owner ───►  League Dashboard tile (IncompletePredictionsTile)
                   Filtered to that league's approved members
```

Both read the same per-user completion data; the admin view is unfiltered, the league view is
filtered to `LeagueMembers`. The reminder **send** action lives behind both, but always dedupes
per `(RoundId, UserId)`.

### Access scope (decided)

**Admins + league owners can send; all league members can view.** Split the two operations:

- **View (read):** the global admin round view is admin-only; the league-dashboard tile is
  visible to **any approved member** of that league (read-only). Members can see who in their
  league is incomplete - this is intentional transparency / social pressure. Auth for the
  league read path: `EnsureApprovedMemberAsync(leagueId, currentUserId)`.
- **Send (write):** only the **league owner** (or a site admin) can trigger reminders. Auth:
  `EnsureLeagueAdministratorAsync(...)` for the league path, `[Authorize(Roles = Administrator)]`
  for the admin path. The "Remind" controls render only for the owner; members see the list
  without action buttons.

> Privacy note: because the tile is member-visible, every member's per-round completion status
> is exposed to their league. That is the accepted trade-off for this decision - it is not a
> leak of prediction *content* (scores stay hidden until deadline per existing rules), only of
> whether each player has finished entering.

## What we reuse (do NOT rebuild)

- **Email send:** `IEmailService.SendTemplatedEmailAsync(to, templateId, parameters)`
  (`BrevoEmailService`). The global email on/off switch and the dev allow-list are already
  enforced inside it.
- **Brevo template 9 (`PredictionsMissing`)** with the merge params the scheduled reminder
  already sends: `FIRST_NAME`, `ROUND_NAME`, `DEADLINE`,
  `PREDICTIONS_URL` (`{baseUrl}/predictions/{roundId}`). Content is identical, so no new
  template is needed (see Open Questions if a dedicated ad-hoc template is wanted).
- **The "predictable match" predicate** already in
  `ReminderService.GetUsersMissingPredictionsAsync` (teams confirmed, not `Postponed`, not
  locked via `CustomLockTimeUtc`, no existing `UserPrediction`). This is exactly the Round 5
  scenario. **Extract it into shared SQL** so the new completion query and the existing
  reminder service cannot drift apart.
- **Notification-log pattern:** mirror `PrizeNotification` / `LeagueWelcomeNotification`
  (domain entity + `Create` factory + repository + unique index).
- **UI patterns:** standard league-dashboard tile skeleton (`LeagueId` param,
  `_isLoading`/`_errorMessage`, spinner/empty states); `ApiError`/`ApiSuccess`; busy-spinner
  buttons; `JsRuntime` `confirm` for the "send to N players?" step.

## Acceptance criteria

- [ ] Admin can open a per-round completion view listing every participant with `X / N`
      predicted, missing count, and the specific missing fixtures.
- [ ] All approved members of a league see an equivalent tile on the league dashboard, scoped
      to that league's approved members, for the active/in-progress round (read-only); the
      "Remind" controls render only for the league owner.
- [ ] View defaults to incomplete players only (Partial + None), Partial sorted first, with a
      toggle to show everyone.
- [ ] Per-row "Remind" and a bulk "Remind all incomplete" both send Brevo template 9 to the
      targeted players.
- [ ] A player is not reminded more than once per `(RoundId, UserId)` within the throttle
      window, regardless of how many admins/owners trigger it.
- [ ] Reminder send is refused once the round deadline has passed.
- [ ] Only players who still have *actionable* missing matches (unlocked) are emailed.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Data layer | Migration `0003`, `PredictionReminderNotification` entity + factory + repository, schema doc + DatabaseTools update | Complete |
| 2 | Application - query | `GetRoundCompletionQuery` handler (missing fixtures returned inline per player), shared predictable-match predicate | Complete |
| 3 | Application - command | `SendPredictionRemindersCommand` with 6h throttle + deadline guard, reusing template 9 | Complete |
| 4 | API endpoints | Admin (`/api/admin/rounds/{id}/completion` + `/reminders`) and league-scoped equivalents | Complete |
| 5 | Contracts | `RoundCompletionDto`, `RoundCompletionPlayerDto`, `MissingFixtureDto`, `SendPredictionRemindersRequest/ResultDto` | Complete |
| 6 | Web - admin page | `Components/Pages/Admin/Rounds/Completion.razor` + "Completion" button on Rounds/List | Complete |
| 7 | Web - league tile | `IncompletePredictionsTile.razor` on the League Dashboard (all members read-only; Remind controls owner-only) | Complete |
| 8 | Tests + coverage | Domain 100% (entity/factory); command + query handler tests | Complete |

> Implementation note: the completion query returns each player's missing fixtures inline (no
> separate `GetUserMissingFixturesQuery` was needed). The "predictable match" predicate is
> duplicated (with a cross-reference comment) between `GetRoundCompletionQueryHandler` and
> `ReminderService` rather than extracted into shared SQL - kept in lockstep by comment.

## Data model

New table `PredictionReminderNotifications` (mirrors `PrizeNotifications` /
`LeagueWelcomeNotifications`):

| Column | Type | Notes |
|--------|------|-------|
| Id | int IDENTITY | PK |
| RoundId | int NOT NULL | FK -> Rounds |
| UserId | nvarchar(450) NOT NULL | FK -> AspNetUsers |
| LastRemindedUtc | datetime2 NOT NULL | updated on each send (upsert) |
| RemindedByUserId | nvarchar(450) NOT NULL | who triggered it (audit) |

- UNIQUE `UX_PredictionReminderNotifications_RoundUser` on `(RoundId, UserId)` - this is the
  dedup key; a send is an upsert of `LastRemindedUtc`.
- Migration `tools/ThePredictions.DatabaseTools/Migrations/0003_CreatePredictionReminderNotifications.sql`
  - **additive**, safe to deploy ahead of code.
- Add `"PredictionReminderNotifications"` to `TableCopyOrder` in `DatabaseRefresher.cs` next to
  the other notification tables. No anonymisation (no personal data - matches how
  `PrizeNotifications` is treated). Update `docs/guides/database-schema.md`.

## Technical notes

- Query handlers use `IApplicationReadDbConnection`; the write path (upserting the log) uses a
  repository (CQRS rule). Positional result `record`s must match `SELECT` column order.
- The completion denominator is "predictable" matches (teams confirmed, unlocked, not
  postponed); "missing" = predictable matches with no `UserPrediction`. Optionally also expose
  "missed & locked" as read-only info, but the actionable number drives reminders.
- Throttle window and the send-time deadline guard live in the command handler.

## Resolved decisions

- **Throttle window:** **6 hours** per `(round, user)` - enforced in the command handler
  regardless of who triggers the send.
- **Template:** **reuse Brevo template 9 (`PredictionsMissing`)** - no new template.
- **League tile audience:** **all approved members** see the tile (read-only); **send is
  owner/admin-only** (see Access scope above).
