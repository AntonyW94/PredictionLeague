# Checklist: Adding a New CSS File

Use this checklist when adding a new CSS file to the Blazor client.

## Step 1: Create the CSS File

Create your file in the appropriate location:

```
wwwroot/css/
├── variables.css          → Design tokens only
├── utilities/             → Reusable utility classes
├── components/            → Component-specific styles
├── layout/                → Layout and structural styles
└── pages/                 → Page-specific styles (last resort)
```

## Step 2: Update Development Import

Add `@import` to `PredictionLeague.Web.Client/wwwroot/css/app.css`:

```css
/* app.css - Add in correct load order */
@import 'variables.css';
@import 'utilities/spacing.css';
@import 'utilities/typography.css';
@import 'components/buttons.css';
@import 'components/your-new-file.css';  /* <-- Add here */
@import 'layout/navigation.css';
@import 'pages/dashboard.css';
```

## Step 3: Update Production Bundle

Add to `<CssFilesToBundle>` in `PredictionLeague.Web.csproj`:

```xml
<ItemGroup Label="CSS files to bundle (in order)">
  <CssFilesToBundle Include="$(BlazorClientWwwroot)/css/variables.css" />
  <CssFilesToBundle Include="$(BlazorClientWwwroot)/css/utilities/spacing.css" />
  <CssFilesToBundle Include="$(BlazorClientWwwroot)/css/components/your-new-file.css" />  <!-- Add here -->
  <!-- ... other files ... -->
</ItemGroup>
```

**IMPORTANT:** The order must match `app.css` imports.

## Step 4: Follow CSS Rules

Your new CSS file must follow these rules:

### Use Design Tokens

```css
/* CORRECT */
.my-component {
    color: var(--text-primary);
    background: var(--purple-800);
    padding: var(--spacing-4);
    border-radius: var(--radius-md);
}

/* WRONG - hardcoded values */
.my-component {
    color: white;
    background: #1a1a2e;
    padding: 16px;
    border-radius: 8px;
}
```

### Use Numeric Colour Scale

```css
/* CORRECT */
.text-green-600 { color: var(--green-600); }
.bg-purple-800 { background: var(--purple-800); }

/* WRONG - old naming */
.text-green { color: var(--green); }
.text-success { color: var(--success); }
```

### Mobile-First Media Queries

```css
/* CORRECT - base styles for mobile, then enhance */
.my-component {
    padding: var(--spacing-2);  /* Mobile */
}

@media (min-width: 768px) {
    .my-component {
        padding: var(--spacing-4);  /* Tablet+ */
    }
}

/* WRONG - max-width */
@media (max-width: 767px) {
    .my-component {
        padding: var(--spacing-2);
    }
}
```

### UK English Spelling

```css
/* CORRECT */
.colour-picker { }
.text-centre { }

/* WRONG */
.color-picker { }
.text-center { }
```

## Step 5: Verify the Bundle

```bash
dotnet publish PredictionLeague.Web -c Release -o ./publish-test
```

Check:
- [ ] `./publish-test/wwwroot/css/app.css` contains your new CSS
- [ ] `./publish-test/wwwroot/css/` has no subdirectories
- [ ] `./publish-test/wwwroot/index.html` has `app.css?v=TIMESTAMP`

## Verification Checklist

- [ ] File created in correct directory
- [ ] `@import` added to `app.css` (development)
- [ ] Entry added to `<CssFilesToBundle>` in `.csproj` (production)
- [ ] Load order is correct in both places
- [ ] Uses design tokens (no hardcoded colours/spacing)
- [ ] Uses numeric colour scale (`.text-green-600` not `.text-green`)
- [ ] Uses mobile-first `min-width` media queries
- [ ] Uses UK English spelling
- [ ] Production bundle verified
