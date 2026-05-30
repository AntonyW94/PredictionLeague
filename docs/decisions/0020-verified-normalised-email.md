# 0020. Verified, normalised email identity (block plus-aliases)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** security, technical, legal

## Context

Email confirmation is half-built: the `EmailConfirmed` column and Dapper user-store support exist and Google-login users are set confirmed, but there is **no confirmation token, no send-on-register, no confirm endpoint, and login does not check `EmailConfirmed`**. Uniqueness relies only on Identity's `ToUpperInvariant` normalisation, so `you+tag@gmail.com` and `you@gmail.com` are treated as **different accounts** — an easy way to farm repeated free trials (0006) now that money is involved.

## Decision

1. **Complete email verification:** send a confirmation token on registration (mirroring the `PasswordResetToken` pattern), add a confirm command + `/confirm-email` endpoint + Brevo template, and **require a confirmed email before purchasing or taking part** (and gate login, soft or hard).
2. **Normalise email for the uniqueness check:** lowercase and **strip the `+suffix`** from the local part to a normalised key, so plus-aliases collapse to one account. Store the **entered** address for delivery; use the normalised key for dedup.

## Consequences

**For / positive**
- Cuts free-trial abuse via alias accounts; reliable deliverability and account recovery; stronger identity for paid accounts.

**Against / cost**
- Registration UX gains a confirm step; build token + endpoint + template + login gate.
- Universal `+`-stripping slightly over-merges on the rare provider that treats `+tags` as distinct mailboxes — accepted trade-off.
- Existing unconfirmed accounts need handling (grandfather, or prompt to confirm).

**Neutral / notes**
- Gmail also ignores dots — optionally strip dots for `gmail.com` in the dedup key (flagged, not required).
- Google-OAuth users are already confirmed.

## Alternatives considered

- **Leave verification off** — rejected; abuse + deliverability risk.
- **Block any `+` in emails outright** — rejected; `+` addresses are legitimate; better to normalise for dedup while still delivering to the entered address.

## Related

- 0006; `season-passes/18-email-verification-and-identity.md`
