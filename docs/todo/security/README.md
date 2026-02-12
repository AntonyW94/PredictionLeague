# Security

This folder contains security improvement plans and deferred security items.

## Deferred Items

These items have been reviewed and deferred with documented mitigations:

| Item | Reason | Mitigations |
|------|--------|-------------|
| [open-redirect](open-redirect/) | Waiting for login system changes | N/A - to be fixed |
| [jwt-security-hardening](jwt-security-hardening/) | Waiting for login system changes | N/A - to be fixed |
| [refresh-tokens-in-urls](refresh-tokens-in-urls/) | Mobile browser compatibility | HTTPS, short expiry, rotation |
| [localstorage-tokens](localstorage-tokens/) | Blazor WASM architecture | Strong CSP, short expiry, XSS prevention |
| [server-validation-gap](server-validation-gap/) | FluentValidation.AspNetCore deprecated | Client validation, domain guards, DB constraints |

## Planned Improvements

| Item | Priority | Description |
|------|----------|-------------|
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
