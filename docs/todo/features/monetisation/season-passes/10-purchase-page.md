# Task: Purchase Page

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Build the Blazor Season Pass purchase page from the mockup: two tiers (Entry / Entry + SMS), launches Stripe Checkout, and shows a free-trial state for eligible users.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Web.Client/Components/Pages/Passes/SeasonPass.razor` | Create | Purchase page (route e.g. `/passes/{seasonId:int}`) |
| `...Services/Passes/SeasonPassStateService.cs` | Create | State + API calls (state-service pattern) |
| `src/ThePredictions.Web.Client/wwwroot/css/components/season-pass.css` | Create | Card styling (design tokens) |
| `src/ThePredictions.Web.Client/wwwroot/css/app.css` | Modify | `@import` the new CSS |
| `src/ThePredictions.Web/ThePredictions.Web.csproj` | Modify | Add CSS to `<CssFilesToBundle>` |

## Implementation Steps

### Step 1: State service

- Loads: season name/metadata, prices per tier, and **trial eligibility** for the current user (from a query backed by `IApplicationReadDbConnection`).
- `BuyAsync(tier)` → calls API `CreateCheckoutSessionCommand`, redirects to the returned Stripe URL.

### Step 2: Page UI (match mockup)

- Two cards: **Season Entry** and **Season Entry + SMS** ("Best value" badge), feature lists, prices, CTA buttons.
- If **trial-eligible**: replace prices with a "Your first season is on us — join free" state and a single CTA that proceeds without payment.
- Follow Web.Client `CLAUDE.md`: state-service + `OnStateChange`/`IDisposable`, design tokens, **mobile-first `min-width`** media queries, verify **light + dark** mode.

### Step 3: Redirect entry points

- When a user hits the access gate (Task 08 → `SeasonPassRequiredException`), the client routes them to `/passes/{seasonId}`.

## Code Patterns to Follow

State-service + component pattern from `src/ThePredictions.Web.Client/CLAUDE.md`. New CSS file checklist: `docs/guides/checklists/new-css-file.md`.

## Verification

- [ ] Renders correctly in light and dark mode, mobile and desktop.
- [ ] Entry / Entry + SMS launch Checkout with the correct tier.
- [ ] Trial-eligible users see the free state and never reach Checkout.
- [ ] Production CSS bundling includes the new file (`dotnet publish` check).

## Edge Cases to Consider

- Free seasons (World Cup) should not surface a purchase page at all.
- User who already holds a pass should be sent to the league, not the purchase page.

## Notes

Reference mockup: `season-pass-mockup.html`. Prices come from server/config, not hardcoded in the component.
