# 0001. Decision Records process

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** process

## Context

A planning session produced many consequential, interlinked decisions — monetisation model, legal positioning on gambling, business structure, payment design — where the *reasoning* is as important as the outcome and easy to forget. The project already uses structured docs (`docs/guides`, `docs/todo/features`) and a `CLAUDE.md` rulebook.

## Decision

Keep lightweight **Decision Records** in `docs/decisions/`, following the ADR convention but covering product, business, legal, financial and technical decisions. Records are **thematic** (a cohesive area of decisions per file) rather than one micro-decision each, to stay navigable for a solo project. Each captures context, the decision(s), for/against, and rejected alternatives.

**Supersede policy:** only supersede a record once a decision has actually **taken effect** (merged to the main branch / implemented). While still drafting in a feature branch, **edit in place** — don't create a supersede chain (or a numbering gap) for a decision that never went live.

## Consequences

**For / positive**
- Preserves rationale for future-you, collaborators, and advisors (solicitor/accountant).
- Prevents re-opening settled questions.
- Thematic grouping keeps the set small and readable.

**Against / cost**
- Small ongoing discipline to write and keep current.
- Bigger records are slightly less granular to supersede (acceptable trade-off vs file sprawl).

## Alternatives considered

- **No formal record (rely on chat history / memory)** — rejected; rationale gets lost, especially legal reasoning.
- **One-decision-per-ADR (classic style)** — rejected here; ~20 micro-records for one feature area added more friction than value. Thematic grouping chosen instead.
- **One big "decisions" page** — rejected; harder to supersede and cross-reference individual themes.

## Related

- All subsequent records (0002+); `docs/decisions/README.md` (index + supersede policy).
