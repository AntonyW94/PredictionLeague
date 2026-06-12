# Code Consistency Audit

## Status

Audit: Not Started | In Progress | **Complete** (2026-06-12)
Remediation: **Not Started** | In Progress | Complete

## Summary

Audit the codebase to ensure all code follows the same patterns and standards defined in CLAUDE.md.

The audit was performed on 2026-06-12 as a full read-through of every project. Findings, the product-owner decisions made against each one, and the prioritised remediation plan are in:

**[2026-06-code-review-findings.md](2026-06-code-review-findings.md)**

Headline finding: server-side FluentValidation never executes (validators target Contracts request DTOs while the MediatR pipeline validates commands) - agreed as a gap to fix first.

## Priority

**High** (from roadmap)

## Requirements

- [x] Review all handlers follow CQRS patterns
- [x] Review all entities follow domain model patterns
- [x] Review all validators follow naming conventions
- [x] Review all SQL follows conventions (brackets, PascalCase)
- [x] Review all logging follows format guidelines
- [x] Review UK English spelling throughout
- [x] Review DateTime.UtcNow usage (no DateTime.Now)

## Areas to Audit

| Area | Reference |
|------|-----------|
| Code style | `docs/guides/code-style.md` |
| CQRS patterns | `docs/guides/cqrs-patterns.md` |
| Domain models | `docs/guides/domain-models.md` |
| Database/SQL | `docs/guides/database.md` |
| Logging | `docs/guides/logging.md` |

## Checklist

See `docs/guides/checklists/security-audit.md` for audit process.

## Technical Notes

Consider creating a Roslyn analyser for automated enforcement of some rules (would catch several of the drift categories found: missing table aliases, direct DateTime.UtcNow, single-line `if`).
