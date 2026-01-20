# Task 4: Create Placeholder SVG

## Objective

Create an SVG placeholder image to display while team logos load and as a fallback for broken/missing logo URLs.

## File to Create

**Path:** `PredictionLeague.Web/PredictionLeague.Web.Client/wwwroot/images/team-placeholder.svg`

## Implementation

Create the following SVG file:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24">
  <circle cx="12" cy="12" r="10" fill="#4a4a6a" stroke="#6a6a8a" stroke-width="1"/>
  <path d="M12 4 L12 20 M4 12 L20 12" stroke="#6a6a8a" stroke-width="0.5" opacity="0.5"/>
  <circle cx="12" cy="12" r="4" fill="#6a6a8a" opacity="0.6"/>
</svg>
```

## Design Rationale

### Visual Design
- **Shape:** Simple circle (resembles a football/badge)
- **Colors:** Purple/grey tones to match the app's dark purple theme
- **Size:** 24x24 viewBox, scales well to 20px display size
- **Minimal:** Clean design that doesn't distract from real team logos

### Color Choices
- `#4a4a6a` - Dark purple fill (matches card backgrounds)
- `#6a6a8a` - Lighter purple stroke/details (subtle contrast)

### Why SVG?
- Infinitely scalable (crisp at any size)
- Tiny file size (~300 bytes)
- Loads instantly (no network request latency)
- Can be styled with CSS if needed in future

## Alternative Simpler Design

If the above design is too complex, use this minimal version:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24">
  <circle cx="12" cy="12" r="10" fill="#4a4a6a" stroke="#6a6a8a" stroke-width="1"/>
</svg>
```

This is just a simple circle with border.

## Usage in Component

The placeholder will be used in two ways:

1. **Default for null URLs:**
   ```html
   <img src="@(match.HomeTeamLogoUrl ?? "images/team-placeholder.svg")" />
   ```

2. **Fallback for broken images:**
   ```html
   <img onerror="this.src='images/team-placeholder.svg'" />
   ```

## Verification

After creating the file:

1. Verify the file exists:
   ```bash
   ls -la PredictionLeague.Web/PredictionLeague.Web.Client/wwwroot/images/team-placeholder.svg
   ```

2. Open the SVG in a browser to verify it renders correctly

3. Check the file size is small (should be under 500 bytes)

## File Location Context

The images folder already contains:
```
wwwroot/images/
├── Logo.png
├── lion-outline-logo.jpg
├── apple-touch-icon-*.png (various sizes)
├── boosts/
│   ├── double-up-normal.png
│   ├── double-up-selected.png
│   └── ... (other boost icons)
└── team-placeholder.svg (NEW)
```

## Next Task

Proceed to [Task 5: Update RoundCard Component](./05-update-round-card-component.md)
