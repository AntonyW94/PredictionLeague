# Blazor Client Guidelines

Rules specific to the Blazor WebAssembly client. For solution-wide patterns, see the root [`CLAUDE.md`](../../CLAUDE.md).

## State Management

Services hold state and notify components via events:

```csharp
public class DashboardStateService
{
    public IEnumerable<MyLeagueDto> MyLeagues { get; private set; } = [];
    public bool IsLoading { get; private set; }

    public event Action? OnStateChange;

    public async Task LoadMyLeaguesAsync()
    {
        IsLoading = true;
        OnStateChange?.Invoke();

        MyLeagues = await _apiClient.GetMyLeaguesAsync();

        IsLoading = false;
        OnStateChange?.Invoke();
    }
}
```

### Component Pattern

```csharp
@inject DashboardStateService DashboardState
@implements IDisposable

@if (DashboardState.IsLoading)
{
    <LoadingSpinner />
}
else
{
    @foreach (var league in DashboardState.MyLeagues)
    {
        <LeagueCard League="league" />
    }
}

@code {
    protected override async Task OnInitializedAsync()
    {
        DashboardState.OnStateChange += StateHasChanged;
        await DashboardState.LoadMyLeaguesAsync();
    }

    public void Dispose()
    {
        DashboardState.OnStateChange -= StateHasChanged;
    }
}
```

## Authentication Flow

1. `ApiAuthenticationStateProvider` checks localStorage for `accessToken`
2. Validates JWT expiration
3. Auto-refreshes expired tokens via `/api/auth/refresh-token`
4. Sets `Authorization: Bearer {token}` header on HttpClient

## CSS Architecture

**Full CSS reference:** [`/docs/guides/css-reference.md`](../../docs/guides/css-reference.md)

### File Structure

```
wwwroot/css/
├── variables.css          → Design tokens (colours, spacing, radii)
├── app.css                → Global styles and imports
├── utilities/             → Reusable utility classes
├── components/            → Component-specific styles
├── layout/                → Layout and structural styles
└── pages/                 → Page-specific styles (last resort)
```

### Design Tokens (ALWAYS Use)

```css
/* CORRECT - use design tokens */
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
}
```

### Colour Scale (Numeric, Tailwind-style)

Higher number = darker colour.

| Scale | Meaning | Example Use |
|-------|---------|-------------|
| 100-300 | Lightest | Accents, highlights |
| 500 | Base | Default usage |
| 600-700 | Dark | Text, emphasis |
| 800-1000 | Darkest | Backgrounds |

```css
/* CORRECT - numeric scale */
.text-green-600 { color: var(--green-600); }
.bg-purple-800 { background: var(--purple-800); }
.text-blue-500 { color: var(--blue-500); }

/* WRONG - old naming (deprecated) */
.text-green { }      /* Use .text-green-600 */
.text-success { }    /* Use .text-green-600 */
.text-cyan { }       /* Use .text-blue-500 */
```

### Mobile-First Media Queries

**ALWAYS use `min-width`. NEVER use `max-width`.**

```css
/* CORRECT - mobile first */
.element {
    padding: var(--spacing-2);  /* Mobile base */
}

@media (min-width: 576px) {
    .element {
        padding: var(--spacing-3);  /* Phone+ */
    }
}

@media (min-width: 768px) {
    .element {
        padding: var(--spacing-4);  /* Tablet+ */
    }
}

@media (min-width: 992px) {
    .element {
        padding: var(--spacing-6);  /* Desktop+ */
    }
}

/* WRONG - max-width */
@media (max-width: 767px) {
    .element { }  /* NEVER do this */
}
```

### Breakpoints

| Breakpoint | Min-width | Target |
|------------|-----------|--------|
| Small phone+ | 480px | Larger phones |
| Phone+ | 576px | Standard phones |
| Tablet+ | 768px | Tablets |
| Desktop+ | 992px | Desktops |

## CSS Things to NEVER Do

### Never Use Old Colour Classes

```css
/* WRONG */
.text-green { }
.bg-green { }
.text-cyan { }
.text-success { }
.text-danger { }

/* CORRECT */
.text-green-600 { }
.bg-green-600 { }
.text-blue-500 { }
.text-green-600 { }
.text-red { }
```

### Never Hardcode Colours

```css
/* WRONG */
color: white;
color: #ffffff;
background: rgba(0, 0, 0, 0.35);

/* CORRECT */
color: var(--white);
background: var(--black-alpha-35);
```

### Never Use max-width Media Queries

```css
/* WRONG */
@media (max-width: 767px) { }

/* CORRECT */
@media (min-width: 768px) { }
```

### Never Put Component Styles in Page Files

Create proper component CSS files in `/components/`.

## Adding New CSS Files

When adding a new CSS file, update TWO places:

1. **Development:** Add `@import` to `wwwroot/css/app.css`
2. **Production:** Add to `<CssFilesToBundle>` in `src/PredictionLeague.Web/PredictionLeague.Web.csproj`

See [`docs/guides/checklists/new-css-file.md`](../../docs/guides/checklists/new-css-file.md) for the full checklist.

## CSS Bundling (Production)

An MSBuild target bundles CSS during `dotnet publish`:

1. Concatenates all CSS files into single `app.css`
2. Prepends Google Fonts import
3. Deletes individual CSS files
4. Adds cache busting: `app.css?v=TIMESTAMP`

Verify with:
```bash
dotnet publish src/PredictionLeague.Web -c Release -o ./publish-test
```
