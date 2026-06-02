# Task: Section prize + freeze-at-deadline

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Add the `Section` prize category (groups vs knockouts) and the deadline **freeze**
that converts the live scheme into the existing `LeaguePrizeSettings` so the
unchanged settlement engine pays out.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Common/Enumerations/PrizeType.cs` | Modify | Add `Section` |
| `src/.../Features/Admin/Rounds/Strategies/SectionPrizeStrategy.cs` | Create | Award best aggregate per stage using `TournamentRoundMappings.Stages` |
| `src/.../Features/Admin/Rounds/Commands/ProcessPrizesCommandHandler.cs` | Modify | Dispatch the new strategy |
| `src/.../Features/Leagues/Commands/FreezePrizeSchemeCommand.cs` (+Handler) | Create | At the deadline, evaluate scheme at final N → write `LeaguePrizeSettings` |
| `src/.../<scheduler/round-deadline hook>` | Modify | Trigger the freeze when the entry deadline passes |

## Implementation Steps

### Step 1: Section strategy

- Group the season's rounds by stage from `TournamentRoundMappings.Stages`
  (Group stage vs Knockouts), rank members per stage, award per the 50/50 split +
  ladder. Reuse tie-pooling semantics from `OverallPrizeStrategy`.

### Step 2: freeze

- On/after `EntryDeadlineUtc`, evaluate the scheme at the **final** approved
  entrant count and materialise rows into `LeaguePrizeSettings`
  (`PrizeType`, `Rank`, `PrizeAmount`) via `League.DefinePrizes(...)`.
- Idempotent: re-running produces the same settings; guard against double-freeze.
- After freeze, the live projection is replaced by the frozen structure in the UI.

### Step 3: keep settlement unchanged

- `ProcessPrizesCommand` strategies, `Winnings`, `LeaguePayouts` all consume the
  frozen `LeaguePrizeSettings` exactly as today.

## Code Patterns to Follow

- `IPrizeStrategy` strategy pattern (`Features/Admin/Rounds/Strategies/`).
- `League.DefinePrizes()` already sets `HasPrizes` + prize collection.
- Logging: `"League (ID: {LeagueId}) prize structure frozen at {EntrantCount} entrants"`.

## Verification

- [ ] `SectionPrizeStrategy` awards correct per-stage winners (incl. ties).
- [ ] Freeze produces `LeaguePrizeSettings` matching the live breakdown at final N.
- [ ] Freeze is idempotent; no double awards.
- [ ] End-to-end: scheme → freeze → existing `ProcessPrizes` → `Winnings` →
      `LeaguePayouts` unchanged.
- [ ] Domain at 100% coverage; build clean.

## Edge Cases to Consider

- Non-tournament season with Section somehow enabled → blocked upstream (gating);
  defensive guard here too.
- Deadline already passed for the legacy WC league when the scheme is first set →
  freeze immediately (or on next round processing).
- Tie pooling interaction with £5 rounding on Overall.

## Notes

This is the bridge between the new live scheme and the existing, proven payout
machinery — deliberately the **last** task so nothing downstream changes until the
new path is ready.
