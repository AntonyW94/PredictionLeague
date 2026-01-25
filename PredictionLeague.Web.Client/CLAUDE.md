# Blazor Client - Project Guidelines

This file contains guidelines specific to the Blazor WebAssembly client. For solution-wide patterns, see the root [CLAUDE.md](../CLAUDE.md).

## State Management

Services hold state and notify components via events:

```csharp
public class DashboardStateService
{
    public event Action? OnStateChange;

    public async Task LoadMyLeaguesAsync()
    {
        // Load data...
        OnStateChange?.Invoke();
    }
}
```

## Authentication Flow

1. `ApiAuthenticationStateProvider` checks localStorage for `accessToken`
2. Validates JWT expiration
3. Auto-refreshes expired tokens via `/api/auth/refresh-token`
4. Sets `Authorization: Bearer {token}` header on HttpClient

## CSS Architecture

**Full CSS reference:** [`/docs/css-reference.md`](../docs/css-reference.md) - Design tokens, utility classes, component patterns

### File Structure

```
wwwroot/css/
├── variables.css          → Design tokens (colours, spacing, radii)
├── app.css                → Global styles and imports
├── utilities/             → Reusable utility classes
├── components/            → Reusable component styles
├── layout/                → Layout and structural styles
└── pages/                 → Page-specific styles (last resort)
```

### Colour Naming Convention

**Use numeric scale (Tailwind-style) for colours with multiple shades:**

| Scale | Meaning | Example |
|-------|---------|---------|
| 100-300 | Lightest | Accents, highlights |
| 500 | Base | Default usage |
| 600-700 | Dark | Text, emphasis |
| 800-1000 | Darkest | Backgrounds |

**Higher number = darker colour.** Example: `--purple-800` (background) is darker than `--purple-300` (accent).

### Mobile-First CSS Approach

All CSS uses mobile-first media queries with `min-width`. Base styles target mobile, then enhance for larger screens.

```css
.element { /* Base mobile styles */ }

@media (min-width: 480px) { /* Small phone+ */ }
@media (min-width: 576px) { /* Phone+ */ }
@media (min-width: 768px) { /* Tablet+ */ }
@media (min-width: 992px) { /* Desktop+ */ }
```

**Never use `max-width` queries.**

### CSS Rules to Follow

1. **Always use design tokens** - Never hardcode colours, use `var(--colour-xxx)`
2. **Use numeric colour scale** - `.text-green-600` not `.text-green`
3. **Prefer utilities over custom CSS** - Check utilities folder first
4. **Keep component CSS focused** - One component per file in `/components/`
5. **Page styles are last resort** - Only for truly page-specific styles
6. **Maintain complete utility sets** - Don't remove unused utilities
7. **Use mobile-first media queries** - Always `min-width`, never `max-width`

### CSS Things to Avoid

1. **Never use old colour class names:**
   - ❌ `.text-green`, `.bg-green`, `.text-cyan`
   - ✅ `.text-green-600`, `.bg-green-600`, `.text-blue-500`

2. **Never use deprecated aliases:**
   - ❌ `.text-success`, `.text-danger`, `.centre`
   - ✅ `.text-green-600`, `.text-red`, `.text-center`

3. **Never hardcode colours:**
   - ❌ `color: white;` or `rgba(0, 0, 0, 0.35)`
   - ✅ `var(--white)` or `var(--black-alpha-35)`

4. **Never put component styles in page files** - Create proper component CSS

5. **Never use max-width media queries**
