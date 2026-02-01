# Task 9: Add Forgot Password Link to Login Page

**Parent Feature:** [Password Reset Flow](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add a "Forgot password?" link to the login page that navigates users to the forgot password page.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web.Client/Components/Pages/Authentication/Login.razor` | Modify | Add forgot password link |

## Implementation Steps

### Step 1: Add Forgot Password Link

Add the "Forgot password?" link after the password input field, before the login button.

**Current code (around line 67-68):**
```razor
                <StyledValidationMessage For="@(() => _model.Password)" />
            </div>

            <div class="d-grid mt-4">
```

**Updated code:**
```razor
                <StyledValidationMessage For="@(() => _model.Password)" />
            </div>

            <div class="text-end mb-3">
                <NavLink href="/authentication/forgot-password" class="text-white-50 small text-decoration-none">
                    Forgot password?
                </NavLink>
            </div>

            <div class="d-grid mt-4">
```

## Visual Result

```
┌─────────────────────────────────────────────────┐
│           ← Back to Home                        │
│                                                 │
│                  [Lion Logo]                    │
│                                                 │
│                    Login                        │
│                 Welcome back                    │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │        Continue with Google             │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
│                    ── OR ──                     │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │ Email address                           │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │ Password                            👁  │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
│                          Forgot password? ←     │  ← NEW
│                                                 │
│   ┌─────────────────────────────────────────┐   │
│   │               Login                     │   │
│   └─────────────────────────────────────────┘   │
│                                                 │
└─────────────────────────────────────────────────┘
```

## Code Patterns to Follow

### Link Styling

Follow the existing styling patterns in auth pages:

```razor
@* Subtle link styling - matches other helper text *@
<NavLink href="/path" class="text-white-50 small text-decoration-none">
    Link text
</NavLink>
```

### Positioning

The link is right-aligned (`text-end`) to follow common UX patterns where "Forgot password?" appears below and to the right of the password field.

## Verification

- [ ] "Forgot password?" link visible on login page
- [ ] Link positioned after password field, before login button
- [ ] Link navigates to `/authentication/forgot-password`
- [ ] Link styling is subtle (matches page aesthetic)
- [ ] Link is accessible (proper contrast, clickable area)
- [ ] Works on mobile devices (touch target size)

## Notes

- The link uses `NavLink` for client-side navigation (no full page reload)
- Styling uses `text-white-50` for a subtle appearance that doesn't distract from the main login flow
- Right-alignment is a common UX pattern for "Forgot password?" links
- The link appears after the password field so users see it when they realise they've forgotten their password
- No additional CSS required - uses existing utility classes
