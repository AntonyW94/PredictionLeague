# Security

This folder contains security improvement plans and deferred security items.

## Deferred Items

These items have been reviewed and deferred with documented mitigations:

| Item | Reason | Mitigations |
|------|--------|-------------|
| [open-redirect](open-redirect/) | Waiting for login system changes | N/A - to be fixed |
| [localstorage-tokens](localstorage-tokens/) | Blazor WASM architecture | Strong CSP, short expiry, XSS prevention |

## Planned Improvements

| Item | Priority | Description |
|------|----------|-------------|
| [server-validation-gap](server-validation-gap/) | High | Enforce FluentValidation server-side at the API boundary (June 2026 audit reversed the January deferral; client-side validation was also found dead) |
| [refresh-tokens-in-urls](refresh-tokens-in-urls/) | High | Replace the raw refresh token in the Google callback URL with a 60-second exchange code (July 2026 review found a mobile-safe design; un-deferred) |
| [account-lockout](account-lockout/) | Medium | Lock accounts after failed login attempts |
| [audit-logging](audit-logging/) | Medium | Security event audit trail |
| [request-security](request-security/) | Medium | Security headers review |
| [suspicious-activity-detection](suspicious-activity-detection/) | Low | Anomaly detection |
| [admin-ip-protection](admin-ip-protection/) | Low | Admin endpoint restrictions |
| [api-key-rotation](api-key-rotation/) | Low | Football API key management |
| [penetration-testing](penetration-testing/) | Low | External security testing |

## Completed Security Work

See [audit-history.md](../../security/audit-history.md) for:
- 34 completed security fixes
- Positive security controls in place

See [accepted-risks.md](../../security/accepted-risks.md) for:
- 3 accepted risks with documentation
- Deferred architectural constraints
- Scanner false positives

## Running a Security Audit

See [security-audit.md](../../guides/checklists/security-audit.md) for the security audit process.
