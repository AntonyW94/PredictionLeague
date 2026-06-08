# Operational Processes

This folder documents **operational processes and runbooks** - the "how do we actually do X"
procedures that aren't code and don't belong in a design guide, an ADR, or a todo plan. If a
task involves external services, credentials access, manual deploy steps, or a repeatable
operational workflow, document it here so it survives across people, sessions, and branches.

This is distinct from the other `docs/` areas:

| Area | Holds |
|------|-------|
| `docs/guides/` | How the codebase works (patterns, conventions, architecture) |
| `docs/decisions/` | Why a decision was made (ADRs) |
| `docs/todo/` | Planned, not-yet-built work |
| `docs/processes/` | **How to carry out an operational process** (this folder) |

## Rules

- **Never commit secret values** (API keys, connection strings, client secrets, tokens). Document
  *where* a secret lives (e.g. Key Vault secret name) and *how* it is accessed, not the value.
- Keep each process in its own file; add it to the index below.

## Processes

| Process | File |
|---------|------|
| Brevo email template management (create/update via API, not the UI) | [brevo-template-management.md](brevo-template-management.md) |
