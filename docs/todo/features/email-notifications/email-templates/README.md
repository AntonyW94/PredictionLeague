# Feature: Email Design System

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped. The house-style design system (purple gradient header, pill hero, bulletproof buttons, Privacy/Terms footer) is applied across all live templates and documented in `docs/email-templates/README.md`.

## Priority

**Phase 1 - Foundation** (per [comms strategy](../../comms-strategy/README.md))

## Summary

A shared design system for every email Brevo sends so all messages look like they come from the same product as the website. Today's templates work but look basic - inconsistent typography, no shared header/footer, no responsive behaviour, no design tokens. This feature establishes a base layout that every existing and future email inherits, and migrates the four current templates onto it.

## User Story

As a user, I want emails from The Predictions to look polished, on-brand, and readable on my phone, so that I trust them as legitimate communication and they don't get lost in my inbox.

As a developer, I want a single base template to maintain so adding a new email is mostly content authoring, not HTML plumbing.

## What "good" looks like

- Distinct header with the site logo and brand name, identical across all emails.
- Body content area with a consistent type stack, button style, link colour, and spacing.
- Footer with copyright, "you received this because..." line, and a link to notification preferences (later).
- Responsive: looks right at 375px (iPhone SE) up to ~1024px wide.
- Plain text version that reads sensibly when HTML is blocked.
- Light and dark email-client friendly (limited - email CSS is constrained, but choose colours that don't break in either).
- Design tokens (colours, spacing, button radius) match the website's `variables.css` so the email feels like the same product.

## Design / Mockup

```
+-------------------------------------------------+
|  [logo]  The Predictions                        |   <- header (purple-800 bg, white text)
+-------------------------------------------------+
|                                                 |
|  Hi {{ firstName }},                            |
|                                                 |
|  [body content per template]                    |
|                                                 |
|  [ Primary CTA Button ]                         |   <- green-600 / white text, rounded
|                                                 |
|  Secondary text or details...                   |
|                                                 |
+-------------------------------------------------+
|  You received this because... | Manage prefs    |   <- footer (light grey, smaller text)
|                                                 |
|  (c) The Predictions                            |
+-------------------------------------------------+
```

## Acceptance Criteria

- [ ] A documented base HTML layout exists and is used by every Brevo template.
- [ ] All four existing templates (`Password Reset`, `Password Reset - Google User`, `Predictions Missing`, `Join League Request`) are rebuilt on the new layout with identical functional behaviour.
- [ ] Emails render correctly in: Gmail (web + iOS), Apple Mail (iOS + macOS), Outlook (web + Windows desktop). The Big Three plus iOS = enough.
- [ ] Emails render correctly at 375px viewport (iPhone SE).
- [ ] All design-token values (colours, spacing, radii) match `variables.css` from the web client where applicable.
- [ ] All emails include a hand-authored plain text version.
- [ ] All emails include alt text for the logo.
- [ ] Existing handler tests continue to pass without modification (template IDs unchanged, only content changes).

## Approach

Brevo supports template inheritance via "shared blocks" / "design library" - to be confirmed during Task 1. If that works, the base layout lives once in Brevo and each template references it. If not, the base layout lives as a copy-pasted HTML stub maintained in the codebase under `docs/email-templates/base.html` and pasted into each Brevo template, with a checklist for keeping them in sync.

Either way, the base layout itself is version-controlled in this repo so changes are reviewed, not just made on Brevo's UI.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Investigate Brevo shared blocks | Confirm whether Brevo supports template inheritance / shared blocks. Pick approach (inherited vs copy-paste). | Not Started |
| 2 | Author base HTML layout | Header + body slot + footer. Tested in major email clients. Committed under `docs/email-templates/base.html`. | Not Started |
| 3 | Author plain-text base | Plain-text equivalent with the same header/footer pattern. | Not Started |
| 4 | Migrate `Password Reset` | Rebuild on new base. Verify via test tool. | Not Started |
| 5 | Migrate `Password Reset - Google User` | Same. | Not Started |
| 6 | Migrate `Predictions Missing` | Same. | Not Started |
| 7 | Migrate `Join League Request` | Same. | Not Started |
| 8 | Document the design system | Add `docs/guides/email-design-system.md` summarising tokens, layout, and how to add a new email. | Not Started |

## Dependencies

- [ ] Logo asset hosted at a stable URL (currently `images/logo.png` in the web client - needs an absolute URL Brevo can fetch).
- [ ] [Admin email test tool](../../admin-moderation/email-test-tool/README.md) - strongly recommended to have first; without it every iteration round-trips through a real workflow trigger. Not strictly blocking.

## Technical Notes

### Email CSS constraints

Email clients only support a subset of CSS. The base layout must use:
- Inline styles or a `<style>` block in `<head>` (Brevo can do either; inline is more reliable).
- Tables for layout where flexbox/grid would otherwise be used (yes, in 2026, still). Outlook is the reason.
- `width` and `height` attributes alongside CSS for image sizing.
- Web-safe fonts with sensible fallbacks (system stack: `-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`). Custom fonts via Google Fonts don't work in Outlook desktop.
- No CSS variables - they're not supported in most email clients. Hardcode the design-token values into the template, with a comment pointing at the source token in `variables.css`.

### Mobile / dark mode

- Use `<meta name="viewport" content="width=device-width, initial-scale=1.0">`.
- Use `@media (max-width: 600px)` overrides (this is one place we *do* use max-width because it's email-client convention).
- Apple Mail and Outlook iOS will auto-invert colours in dark mode; design with that in mind. Avoid pure white backgrounds on body so the inversion is less jarring; pick mid-tones that read well both ways.

### Logo URL

The logo needs to be hosted at a public URL Brevo can fetch. Options:
1. The deployed site's `/images/logo.png` - free but breaks if the site is down when Brevo composes the email.
2. Brevo's own asset hosting - they let you upload images that get served from a Brevo CDN.
3. A simple cloud storage URL.

Recommend option 2 (Brevo asset hosting) for reliability and so emails work even when our site is mid-deploy.

## Open Questions

- [ ] Final decision on shared-blocks vs copy-paste approach (Task 1 output).
- [ ] Should we differentiate the base layout for transactional vs marketing emails? Probably not yet - we have no marketing emails.
- [ ] Hand-author plain text per template, or just paste the HTML body stripped? Hand-authored reads better; recommend doing it properly even though it doubles the per-template work.
