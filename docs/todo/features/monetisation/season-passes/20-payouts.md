# Task: Payouts — player payout details & end-of-league settlement list

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Let players **optionally** store encrypted **payout bank details**, and give league admins an end-of-league **payouts list** (winner, amount, details if shared) with a **manual "mark as paid"** action — so admins can pay winners without chasing each one. Peer-to-peer; the software never moves money (ADR 0010, 0003).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `UserPayoutDetails` + `LeaguePayouts` tables | Create | Encrypted player details; **aggregated per-(league,user) payout** + `PaidAtUtc` (Task 07) |
| `IFieldEncryptionService` | Reuse | From Task 19 (AES, key in Key Vault) |
| `...Account/Commands/SetPayoutDetailsCommand(.Handler).cs` (+ delete) | Create | Player adds/updates/deletes their details |
| `...Queries/GetLeaguePayoutsQuery(.Handler).cs` | Create | Admin-only list: **one row per winner** (total amount) + decrypted details (if shared) + paid flag |
| `...Commands/MarkLeaguePayoutPaidCommand(.Handler).cs` | Create | Admin marks a user's **league total** paid (`LeaguePayouts.PaidAtUtc`) |
| Join flow (Task 19) | Modify | Join-time warning if saved payout details exist (named admin) + "remove" button |
| Account settings UI + league payouts page (admin) | Create | Capture details (with named-admin disclosure); payouts list with mark-as-paid |
| `DataAnonymiser.cs` / `PersonalDataVerifier.cs` | Modify | Scrub/verify `UserPayoutDetails` (Task 07) |

## Implementation Steps

### Step 1: Player payout details (optional, encrypted)
- Account settings: optional **account name / sort code / account number**, encrypted via `IFieldEncryptionService`, stored in `UserPayoutDetails`.
- **Disclosure + consent:** show the player **which admin(s) can see them** — i.e. the named admins of leagues they're an approved member of — and that the details are encrypted and used only for payouts. Allow **delete**.
- Make clear: **if not provided and they win, the admin will message them** for details.

### Step 2: Access control + join-time consent
- Decrypt and return a player's payout details **only** to the **admin of a league that player is an approved member of** (and to the player themselves). Never to other members, never publicly, never in logs.
- **Join-time warning:** when a player joins a **new prize league** and already has saved payout details, show a warning that **that league's admin (named) can now see** them, with a **"remove my saved details"** button at that point (wire into the Task 19 join flow).

### Step 3: Payouts list (admin) — one aggregated total per winner
- A user can have **several `Winnings`** in a league; payouts are tracked **per (league, user)** in `LeaguePayouts` = the **sum** of their winnings, **not** per individual prize.
- `GetLeaguePayoutsQuery` returns **one row per winner**: name, **total amount** (aggregated), **payout details if shared** (else a "contact them" prompt), and **paid?** (`LeaguePayouts.PaidAtUtc`). Populate/refresh `LeaguePayouts` from the `Winnings` aggregation (e.g. when prizes finalise or the page loads).
- `MarkLeaguePayoutPaidCommand` sets `PaidAtUtc` on the user's league total when paid manually. Show outstanding vs paid totals.

### Step 4: Tools
- `DataAnonymiser` replaces `UserPayoutDetails` ciphertext with dummy values on dev refresh; `PersonalDataVerifier` asserts none survive.

## Verification

- [ ] Player can add/edit/delete payout details; stored as ciphertext (not human-readable in DB).
- [ ] Details visible only to admins of leagues the player is in (and the player); denied to others; named-admin disclosure shown before saving.
- [ ] **Join-time warning** appears when joining a prize league with saved details, naming the admin, with a working "remove" button.
- [ ] Payouts list shows **one total per winner** (sum of their winnings) + details (or contact prompt) + paid state; mark-as-paid sets `LeaguePayouts.PaidAtUtc` on the aggregate row.
- [ ] No plaintext payout details in logs or unauthorised responses; dev refresh anonymises them.
- [ ] Domain coverage 100% for new logic.

## Edge Cases to Consider

- Winner with no stored details → "contact them" prompt; admin pays out-of-band.
- **Winnings change after a payout was marked paid** (e.g. results corrected) → recompute the `LeaguePayouts` total and flag a discrepancy rather than silently overwriting `PaidAtUtc`.
- Player leaves all prize leagues → offer to delete details (GDPR purpose-limitation/retention).
- Key rotation (shared concern with Task 19).
- A future **automated** payout system would replace the manual mark-as-paid — **only with legal sign-off** (ADR 0003/0008); the list/`PaidAtUtc` model is the seam for that.

## Related

- ADR 0010, 0003, 0008; Tasks 19 (entry fees, shares encryption), 07 (DB).
