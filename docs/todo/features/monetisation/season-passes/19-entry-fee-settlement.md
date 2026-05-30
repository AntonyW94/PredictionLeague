# Task: Entry-Fee Settlement & Encrypted Admin Bank Details

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Let a league admin store **bank details** against a league (encrypted at rest), and surface them — with the amount and a reference — to players at the point they request to join, so members can settle entry fees **peer-to-peer** smoothly. The software facilitates information and tracking only; it **never touches the money** (ADR 0003, 0010).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/League.cs` | Modify | Add bank-detail fields (stored encrypted) + payment reference template |
| `...Application/Services/IFieldEncryptionService.cs` (+ impl in Infrastructure) | Create | App-level AES encrypt/decrypt; key from Azure Key Vault |
| `Leagues` table | Modify | Encrypted bank-detail columns (Task 07 conventions: schema doc + DatabaseTools) |
| `CreateLeagueCommandHandler` / `UpdateLeagueCommandHandler` | Modify | Accept + encrypt bank details |
| `...Queries/GetLeaguePaymentInfoQuery(.Handler).cs` | Create | Return decrypted details **only** to authorised viewers |
| `JoinLeagueCommandHandler` + join UI / members page | Modify | Show details+amount+reference on request; admin "mark paid"/accept; optional player "I've paid" nudge |
| `DataAnonymiser.cs` / `PersonalDataVerifier.cs` | Modify | Scrub/verify bank details in dev refresh |

## Implementation Steps

### Step 1: Domain + encryption
- Add to `League`: `BankAccountName`, `BankSortCode`, `BankAccountNumber`, optional `PaymentReferenceTemplate` (all optional). Persist **ciphertext**.
- `IFieldEncryptionService` (app-level **AES-GCM**, key in **Key Vault**) encrypts on write, decrypts on read. Never log plaintext.

### Step 2: Access control (least exposure)
- Decrypt and return bank details **only** to: the league **admin**, and **pending/approved members of that league** (for payment). Never to non-members, never on public/browse views, never in API responses to unauthorised users.

### Step 3: Join/pay flow (ADR 0010)
- Entry code is **shared freely**; player **requests to join** → shown the admin's details + **exact amount** + **unique reference** (e.g. their name).
- Optional **"I've paid"** button nudges the admin; **admin accepts** once payment received (never auto-accept on the player's claim).
- If the admin set **no** bank details, fall back to today's "arrange payment manually" behaviour.

### Step 4: Schema + tools
- Add columns to `Leagues` in `docs/guides/database-schema.md`; note they're encrypted.
- `DataAnonymiser`: replace bank details with dummy ciphertext/values in dev refresh; `PersonalDataVerifier`: assert no real bank details survive a refresh.

## Verification

- [ ] Admin can add/edit bank details; stored as ciphertext (verified in DB — not human-readable).
- [ ] Details decrypt only for the admin and that league's pending/approved members; denied to others.
- [ ] Join flow shows amount + reference + details; admin acceptance still required; "I've paid" never auto-accepts.
- [ ] No plaintext bank details in logs or unauthorised API responses.
- [ ] Dev refresh anonymises bank details; verifier passes.
- [ ] Domain coverage 100% for new `League` logic.

## Edge Cases to Consider

- No bank details set → manual fallback; no broken UI.
- Key rotation: design encryption so the Key Vault key can be rotated (versioned keys / re-encrypt path).
- Editing bank details after members joined — allowed; doesn't retroactively change accepted members.
- This is **peer-to-peer**: the platform must never initiate, hold, or route the payment (ADR 0003).

## Related

- ADR 0010, 0003; Tasks 07 (DB conventions), 08 (join flow), 10 (purchase page).
