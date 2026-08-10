# JavaScript Minification

## Status

**Not Started** | In Progress | Complete

## Summary

CSS is minified at publish; JavaScript is not. The same tool already in the build does both, so this is
a small addition to an existing target.

## Priority

**Low.** The saving is modest and the files are cached after the first visit. Worth doing because the
machinery is already there, not because the numbers are compelling.

## The numbers

| File | Size | Notes |
|---|---|---|
| `js/interop.js` | 20 KB | The bulk of it |
| `js/error-handlers.js` | 4 KB | Extracted from `index.html`, August 2026 |
| `js/loading-theme.js` | 4 KB | Extracted from `index.html`, August 2026 |

Roughly 28 KB raw. Minified and gzipped the saving is likely 5 to 8 KB, against the 23 KB the CSS
minification delivered. `interop.js` is loaded on every page, so it is on the critical path, but it is
also cached hard after the first request.

## Approach

`src/ThePredictions.Web/ThePredictions.Web.csproj` already has a `MinifyCss` inline task using **NUglify
1.22.3**, referenced via `GeneratePathProperty` with `PrivateAssets="all"`. NUglify minifies JavaScript
as well (`Uglify.Js`), so this is a sibling task rather than a new dependency.

- [ ] `MinifyJs` inline task alongside `MinifyCss`, same `$(PkgNUglify)` reference and forward-slash path
      so it works on Linux in CI
- [ ] Call it for each file in `wwwroot/js` during `BundleCssAndAddCacheBusting`
- [ ] Fail the build on any minifier error, exactly as `MinifyCss` does - never write a damaged script
- [ ] Log the before and after sizes, matching the CSS task's output

## Verify it did not break anything

JavaScript minification is riskier than CSS: renaming a local variable is safe, but anything reached by
name from outside is not. Two specific hazards here:

- **`interop.js` functions are called by name from .NET** via `IJSRuntime.InvokeAsync("blazorInterop.x")`.
  Those names must survive. Keep the object and its members intact - do not let the minifier mangle
  top-level or exported names.
- **`[JSInvokable]` callbacks** go the other way: `CountdownTimer.razor` passes `"UpdateTimer"` as a string
  for JavaScript to call back into. That is a .NET method name so the minifier cannot touch it, but the
  round trip is worth testing.

Checks after the change:

- [ ] Countdown timers still tick on the dashboard (`blazorInterop.startCountdown`, `stopCountdown`)
- [ ] The UTC date picker still converts (`blazorInterop.getTimezoneOffset`)
- [ ] Every other `blazorInterop` entry point still resolves - grep `InvokeAsync` and `InvokeVoidAsync`
      for the full list and check each
- [ ] No console errors on any main screen

## Do it after, not before

Leave this until the higher-value work is done. If [component logic extraction](../component-logic-extraction/)
happens first, some interop may disappear or move, and minifying a file that is about to be rewritten is
wasted verification.
