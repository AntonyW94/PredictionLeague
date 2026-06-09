# Task 5: Admin Cog Link

**Parent Feature:** [Email Test Tool](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped (PR #103) - admin nav link to the email test tool is in place.

## Goal

Make the new page reachable from the existing admin dropdown.

## Where

[`src/ThePredictions.Web.Client/Components/Layout/NavLayout.razor`](../../../../src/ThePredictions.Web.Client/Components/Layout/NavLayout.razor:31)
- the admin cog dropdown's `<ul class="dropdown-menu dropdown-menu-end">`.

Add a new `<li>` to the existing list, after `Manage Users`:

```razor
<li>
    <NavLink class="dropdown-item" href="/admin/email-tests">
        <span class="bi bi-envelope-paper me-2 fw-bold"></span>Email Test Tool
    </NavLink>
</li>
```

Pick the icon to match the existing visual language (`bi-envelope-paper` or `bi-envelope-gear` from Bootstrap Icons - confirm one of them is available in the project's icon set; if not, fall back to `bi-envelope`).

## Verification

- [ ] As an administrator, the cog dropdown shows a new "Email Test Tool" entry below Manage Users.
- [ ] As a non-administrator, the cog (and therefore the new entry) does not appear.
- [ ] Clicking the entry navigates to `/admin/email-tests`.
- [ ] Visual styling matches the surrounding dropdown items in both themes.
