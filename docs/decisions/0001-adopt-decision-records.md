# 0001. Adopt Decision Records (ADRs)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** process

## Context

A planning session produced many consequential, interlinked decisions — monetisation model, legal positioning on gambling, business structure, payment design — where the *reasoning* is as important as the outcome and easy to forget. The project already uses structured docs (`docs/guides`, `docs/todo/features`) and a `CLAUDE.md` rulebook.

## Decision

We will keep lightweight **Decision Records** in `docs/decisions/`, following the ADR convention but covering product, business, legal, financial and technical decisions. Each records context, the decision, for/against, and rejected alternatives.

## Consequences

**For / positive**
- Preserves rationale for future-you, collaborators, and advisors (solicitor/accountant).
- Prevents re-opening settled questions.
- Low cost; fits existing docs culture.

**Against / cost**
- Small ongoing discipline to write and keep current.
- Risk of staleness if not maintained (mitigated by the `Superseded` status convention).

**Neutral / notes**
- Records are immutable in spirit: reverse a decision with a *new* record, don't rewrite the old one.

## Alternatives considered

- **No formal record (rely on chat history / memory)** — rejected; rationale gets lost, especially legal reasoning.
- **One big "decisions" page** — rejected; harder to supersede and cross-reference individual choices.

## Related

- All subsequent records (0002+).
