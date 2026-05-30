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
- If **trial-eligible**: show a "Your first season is on us — join free" state. The **Entry** CTA proceeds free without payment; an optional **"add SMS for £{uplift}"** choice routes through Checkout for the uplift only (creates a `CreateTrialWithSms` pass).
- If **reward-eligible** (Task 13): offer Entry + SMS at the Entry price with a "you earned free SMS" message.

### Step 2b: No late entry — purchase closes at season start

- Once the season has **started** (its first round deadline has passed), **do not offer purchase or trial** — show an "entries for this season have closed" state instead (ADR 0021). Same cut-off as refunds (ADR 0019).

### Step 2a: Require a valid UK mobile before SMS purchase

- The **Entry + SMS** option (and the trial SMS add-on) is **blocked until the user has a valid UK mobile** on their account.
- Validate with **`libphonenumber-csharp`**: parse region `"GB"`, require `IsValidNumber` **and** `NumberType == Mobile`; store the **E.164** form. (Add a reusable phone validator in the Validators project.)
- If no/invalid number, prompt to add one inline before allowing the SMS purchase. Email-only Entry needs no phone. UK-only — non-UK mobiles are not offered SMS (ADR 0009).
- Also require a **confirmed email** before any purchase (ADR 0020 / Task 18).
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
