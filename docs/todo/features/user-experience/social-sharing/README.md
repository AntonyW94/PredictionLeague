# Social Sharing

## Status

Not Started | **In Progress (image share shipping; per-content OG residual)** | Complete

> **Scope decision (2026-07-27):** the visible share feature is a bet365-style
> **image share** - a branded PNG of the player's own predictions for a round,
> handed to the phone's native share sheet (WhatsApp / Instagram / Messages / etc.)
> via the Web Share API. The image is rendered **server-side** with SkiaSharp
> (MIT licence, Windows native assets in the base package - the production host is
> Windows/IIS) so it is pixel-perfect, consistent across devices, and free of the
> tainted-canvas problem that client-side DOM capture hits with remote team logos.

## Summary

Let players show off their predictions. From the Active Rounds tile, once a player
has predicted a round, a **Share** button generates a branded card of their
predicted scorelines (and, once the round is scoring, how each pick is doing) and
opens the native share sheet so it can be sent to anyone.

## Priority

**Low** (roadmap) - effort tier item 11.

## Delivered in this iteration

- [x] Server-rendered share card: `GET /api/rounds/{roundId}/share-card` returns a
      PNG of the current user's predictions for the round (SkiaSharp renderer in
      Infrastructure behind `IShareCardRenderer`; data via a read-side query).
- [x] Team logos fetched server-side via a typed `HttpClient` and rasterised
      (badges/flags are stored as **SVG**, handled via `Svg.Skia`; raster logos
      decode directly), with a graceful abbreviation-badge fallback when a logo is
      missing or cannot be decoded. Decoded logos are cached (`IMemoryCache`, 12h)
      so renders are fast - important because a slow render pushes the client's Web
      Share call outside its short user-activation window and the first share no-ops.
- [x] Scores shown as tinted **badge/pill** chips matching the website's outcome
      chips (green exact / orange correct-result / red incorrect; neutral pre-result).
- [x] "How I did" state: once a match is scored the card shows the actual score and
      colour-codes the pick (exact / correct-result / incorrect).
- [x] Real brand logo in the card header (embedded resource), with **light and dark**
      colour schemes that mirror the player's selected UI theme: the client passes its
      active theme when Share is tapped, falling back to the user's saved `PreferredTheme`.
- [x] Share button in two places: the Active Rounds `RoundCard` footer (shown once
      the player has predicted) and the league dashboard `RoundResultsTile` (the
      "how I did" brag, shown when the player took part in the selected round). Both
      use the native share sheet with a download fallback.
- [x] Web Share API interop shim (`navigator.canShare` / `navigator.share`).

## Link previews (Open Graph / Twitter)

- [x] **Generic site-wide preview upgraded.** The `index.html` OG/Twitter tags now
      point at a purpose-built branded 1200x630 banner (`images/og-preview.png` -
      logo, tagline, description, URL on the brand background) instead of the bare
      logo, with `og:image:width/height` and `alt` so the large-card unfurl renders
      reliably. This is the preview any link to the site shows.

## Residual (deliberately deferred - low value)

- [ ] **Per-content (per-round) link previews.** Making a pasted link unfurl with a
      *specific round's* card (rather than the generic banner) needs crawler-facing,
      server-rendered HTML in `ThePredictions.Web` plus a public, unauthenticated
      image endpoint (crawlers are not logged in, so it cannot show the sharer's own
      predictions - only a generic round card). Judged low value: the feature already
      shares the rich image directly, and there is no per-round share link in the UI.
- [ ] Share **results** surface beyond the tiles (e.g. a season recap share) -
      overlaps with the `season-recap` plan.

## Notes

- The share-card endpoint is authenticated (bearer token) and only ever renders the
  **calling** user's own predictions - there is no user id in the route.
- Deployment note (additive): SkiaSharp ships Windows native assets in the base
  package, so the framework-dependent Windows/IIS publish needs no extra RID. A
  future move to Linux containers would add `SkiaSharp.NativeAssets.Linux`.
