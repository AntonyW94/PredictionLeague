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
3. Auto-refreshes expired tokens via `/api/authentication/refresh-token`
4. Sets `Authorization: Bearer {token}` header on HttpClient

## CSS Architecture

**Full CSS reference:** [`/docs/guides/css-reference.md`](../../docs/guides/css-reference.md)

### File Structure

```
wwwroot/css/
├── variables.css          → Design tokens (colours, spacing, radii)
├── app.css                → Global styles and imports
├── poppins.css            → Self-hosted font faces (must bundle first)
├── fonts/                 → Poppins woff2 - MUST stay adjacent to poppins.css
├── utilities/             → Reusable utility classes
├── components/            → Component-specific styles
├── layout/                → Layout and structural styles
└── pages/                 → Page-specific styles (last resort)
```

## Static Asset Structure

```
wwwroot/
├── favicon.ico            → Stays at the root; browsers probe /favicon.ico regardless of markup
├── css/                   → Ours, bundled at publish
├── js/                    → Ours
├── lib/                   → Third party, pinned versions, never edited by hand
│   ├── bootstrap/         → 5.3.3
│   ├── bootstrap-icons/   → 1.11.3, with fonts/ adjacent to its CSS
│   └── sweetalert2/       → 11.26.25
└── images/
    ├── brand/             → Logos and the social card
    ├── icons/             → Favicon PNGs and the Apple touch icon
    ├── boosts/            → Boost artwork, one file per state
    ├── content/           → Photography, with licences recorded in its README
    └── placeholders/      → Stand-ins for absent data
```

### Naming

```
{subject}[-{variant}][-{state|background}][-{width}].{ext}
```

Lowercase kebab-case throughout. Five rules:

1. **The folder supplies the category - never repeat it in the filename.** A placeholder in
   `placeholders/` is `team-badge-unknown.svg`, not `team-placeholder.svg`.
2. **Subject first, modifiers after**, most significant first, so related files sort together.
3. **Fixed vocabularies, not free text:**
   | Modifier | Values |
   |---|---|
   | state | `normal`, `selected`, `disabled`, `unknown`, `empty` |
   | mode | `light-mode`, `dark-mode` |
   | logo variant | `mark` (the TP monogram alone), `wordmark` (the words alone), `lockup` (both together) |
   | width | bare pixel number, **only** where several sizes of one image exist |
4. **Describe what it is, never where it is used or what it looks like.** Placement changes and
   appearance is subjective; the subject does not.
5. **Keep external platform conventions verbatim** - `favicon`, `apple-touch-icon`.

#### Why `light-mode`, not `light`

Because the short form is genuinely ambiguous, and the old names were the opposite of the natural
reading. `logo-header-light.png` contained **dark** ink: it meant "for light mode". Anyone reading
"logo-light" would reasonably expect the light-coloured logo, and pick the wrong file.

Spelling out `-mode` removes that: `logo-lockup-light-mode` can only mean the asset for light mode,
never "the light-coloured asset". It also matches the vocabulary the CSS already uses, `themes/light/`,
`themes/dark/` and `.theme-dark`, so there is one word for the concept across the codebase.

**The one place this reads oddly, and it is not a mistake.** `.hero-image` in `layout/layout.css` uses
`logo-lockup-dark-mode.webp` in *both* modes, because the hero sits on a dark purple gradient whatever
the theme and needs the light-ink artwork for contrast. That rule carries a comment saying so. If a
second case like it appears, the honest fix is to name assets after the background they sit on
(`-on-dark`) rather than the mode - but one exception is not worth trading away the consistency with
`themes/`.

#### The two logos must share a canvas

Both are 1536x256 with the artwork centred. They are drawn with `background-size: contain`, so the
canvas aspect ratio decides the rendered size - and when the two canvases differed the same navbar box
rendered the logo at 180px wide in light mode and 162px in dark, an 11% jump on theme switch. If you
replace one, pad it to the same canvas rather than trusting the export. The artwork itself differs
slightly in proportion (about 3%), which is why they are padded to a common canvas rather than scaled
to identical dimensions: stretching a logo to match is worse than a few pixels of transparency.

### Nothing loads from a third party

Every stylesheet, script, font and image comes from our own origin, which is what lets the
Content-Security-Policy set `script-src 'self'` and `style-src 'self'` (see
`SecurityHeadersMiddleware`). **Do not reintroduce a CDN or a hotlinked image.** A CDN in `script-src`
would let an attacker who achieves HTML injection load arbitrary code from an origin the browser has
been told to trust, which is the attack the policy exists to prevent.

Adding a third-party library means vendoring it into `lib/` at a pinned exact version. Adding a
photograph means downloading it, sizing it for the space it occupies, and recording its licence in
`images/content/README.md`.

**Webfonts must sit next to the CSS that declares them.** `css/fonts/` and
`lib/bootstrap-icons/fonts/` look like duplication and are not: both are referenced by a relative
`url()`, and moving them to a shared folder would silently break every icon and every font weight.

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

### Always Verify Both Light and Dark Mode

When adding or changing any UI element (components, styles, icons, colours), verify it looks correct in **both** light mode and dark mode. Check that:
- Text has sufficient contrast against its background in both themes
- Colours use design tokens or existing utility classes that have dark mode overrides (e.g. `text-white-50` is overridden in `themes/dark/dark.css`)
- New CSS classes include `.theme-dark` overrides where needed

## Adding New CSS Files

When adding a new CSS file, update TWO places:

1. **Development:** Add `@import` to `wwwroot/css/app.css`
2. **Production:** Add to `<CssFilesToBundle>` in `src/ThePredictions.Web/ThePredictions.Web.csproj`

See [`docs/guides/checklists/new-css-file.md`](../../docs/guides/checklists/new-css-file.md) for the full checklist.

## CSS Bundling (Production)

An MSBuild target bundles CSS during `dotnet publish`:

1. Concatenates all CSS files into a single `app.css`, starting with `poppins.css` so the font faces
   are declared before anything uses them
2. Deletes the individual CSS files (the webfonts under `css/fonts/` and everything in `lib/` are left
   alone)
3. Adds cache busting: `app.css?v=TIMESTAMP`, and the same stamp to the `js/` files

It **concatenates, it does not minify** - there is no minifier in the build. `wwwroot/css/app.min.css`
is gitignored and, if present, is a stale local leftover that nothing references.

Vendor CSS in `lib/` is deliberately **not** bundled: `bootstrap-icons.min.css` finds its webfont
through a relative `url()`, and concatenating it to a different directory depth would break every icon.

Verify with:
```bash
dotnet publish src/ThePredictions.Web -c Release -o ./publish-test
```
