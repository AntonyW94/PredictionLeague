# Code Consistency Audit

## Status

**Not Started** | In Progress | Complete

## Summary

Audit the codebase to ensure all code follows the same patterns and standards defined in CLAUDE.md.

## Priority

**High** (from roadmap)

## Requirements

- [ ] Review all handlers follow CQRS patterns
- [ ] Review all entities follow domain model patterns
- [ ] Review all validators follow naming conventions
- [ ] Review all SQL follows conventions (brackets, PascalCase)
- [ ] Review all logging follows format guidelines
- [ ] Review UK English spelling throughout
- [ ] Review DateTime.UtcNow usage (no DateTime.Now)

## Areas to Audit

| Area | Reference |
|------|-----------|
| Code style | `docs/claude/code-style.md` |
| CQRS patterns | `docs/claude/cqrs-patterns.md` |
| Domain models | `docs/claude/domain-models.md` |
| Database/SQL | `docs/claude/database.md` |
| Logging | `docs/claude/logging.md` |

## Checklist

See `docs/claude/checklists/security-audit.md` for audit process.

## Technical Notes

Consider creating a Roslyn analyser for automated enforcement of some rules.
