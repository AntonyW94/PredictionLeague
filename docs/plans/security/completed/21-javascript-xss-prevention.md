# Fix Plan: JavaScript XSS Prevention

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P2 - Medium |
| Severity | Medium |
| Type | Cross-Site Scripting (XSS) |
| CWE | CWE-79: Improper Neutralization of Input During Web Page Generation |
| OWASP | A03:2021 Injection |

---

## Vulnerability Details

### Description
User names are interpolated into HTML template literals in JavaScript interop functions without proper sanitisation.

### Affected Files
- `PredictionLeague.Web.Client/wwwroot/js/interop.js` (lines 48-88, 90-129)

### Vulnerable Code

```javascript
// Line 51: User fullName inserted into HTML without escaping
const optionsHtml = userList
    .filter(user => user.id !== userToDeleteId)
    .map(user => `<option value="${user.id}">${user.fullName}</option>`)
    .join('');

// Line 93: userName displayed in title without sanitisation
Swal.fire({
    title: `Change role for ${userName}`,
    // ...
});
```

### Attack Vector
If an attacker manages to get a name like `<img src=x onerror="alert('XSS')">` stored in the database, it would execute when admin views the user list.

### Current Mitigation
The `NameValidator` in `PredictionLeague.Validators` restricts user names to safe characters, significantly reducing this risk. However, defense-in-depth requires output encoding as well.

---

## Fix Implementation

**File:** `PredictionLeague.Web.Client/wwwroot/js/interop.js`

### Step 1: Add Reusable Helper Function

Add this helper function at the top of the `window.blazorInterop` object to safely escape HTML in strings:

```javascript
window.blazorInterop = {
    // Reusable helper function to escape HTML entities
    // Use this whenever inserting user-provided text into HTML templates
    escapeHtml: function(unsafe) {
        if (typeof unsafe !== 'string') return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    },

    // ... existing functions
};
```

### Step 2: Update showReassignLeagueConfirm Function

Use the `escapeHtml` helper when building the select options:

```javascript
showReassignLeagueConfirm: function (message, userList, userToDeleteId) {
    return new Promise((resolve) => {
        const optionsHtml = userList
            .filter(user => user.id !== userToDeleteId)
            .map(user => `<option value="${this.escapeHtml(user.id)}">${this.escapeHtml(user.fullName)}</option>`)
            .join('');

        Swal.fire({
            title: message,  // Safe - comes from C# string literal
            html: `<select id="swal-reassign-select" class="swal2-select">${optionsHtml}</select>`,
            showCancelButton: true,
            confirmButtonText: 'Reassign & Delete',
            cancelButtonText: 'Cancel',
            preConfirm: () => {
                return document.getElementById('swal-reassign-select').value;
            }
        }).then((result) => {
            if (result.isConfirmed && result.value) {
                resolve(result.value);
            } else {
                resolve(null);
            }
        });
    });
},
```

### Step 3: Update showRoleChangeConfirm Function

Use the `escapeHtml` helper for the user name in the title:

```javascript
showRoleChangeConfirm: function (userName, currentRole) {
    return new Promise((resolve) => {
        Swal.fire({
            title: `Change role for ${this.escapeHtml(userName)}`,
            text: `Current role: ${currentRole}`,  // Safe - comes from enum
            input: 'select',
            inputOptions: {
                'Player': 'Player',
                'Administrator': 'Administrator'
            },
            inputValue: currentRole,
            showCancelButton: true,
            confirmButtonText: 'Change Role',
            cancelButtonText: 'Cancel'
        }).then((result) => {
            if (result.isConfirmed && result.value !== currentRole) {
                resolve(result.value);
            } else {
                resolve(null);
            }
        });
    });
}
```

### Usage Guidelines

The `escapeHtml` function should be used whenever:
- User-provided data is inserted into HTML template literals
- Dynamic values from the database are displayed in SweetAlert dialogs
- Any untrusted string is used within HTML content

It does NOT need to be used for:
- Values passed to `text` properties (SweetAlert escapes these automatically)
- Values that come from C# string literals (these are trusted)
- Enum values or other controlled vocabularies

---

## Testing

### Manual Test Steps

1. (In development/test environment only) Temporarily disable NameValidator
2. Create user with name: `<img src=x onerror="alert('XSS')">`
3. Navigate to Admin > Users list
4. Attempt role change or delete operation
5. Verify no alert popup (XSS blocked)
6. Re-enable NameValidator

### Automated Test (if test framework available)

```javascript
describe('XSS Prevention', () => {
    it('should escape HTML in user names', () => {
        const maliciousName = '<script>alert("XSS")</script>';
        const escaped = blazorInterop.escapeHtml(maliciousName);
        expect(escaped).toBe('&lt;script&gt;alert(&quot;XSS&quot;)&lt;/script&gt;');
    });
});
```

---

## Defense in Depth

This fix complements existing protections:

1. **Input Validation** (existing): `NameValidator` restricts allowed characters
2. **Output Encoding** (this fix): JavaScript properly escapes user data
3. **Content Security Policy** (existing): CSP restricts script execution
4. **Blazor Encoding** (existing): Blazor auto-encodes Razor output

All four layers should be in place for comprehensive XSS protection.

---

## Rollback Plan

If the escapeHtml approach causes display issues:
1. Review the escape patterns to ensure all necessary characters are handled
2. Ensure `escapeHtml` is called consistently on all user data
3. Consider using DOM manipulation with `textContent` as an alternative for specific functions

---

## Notes

- The `escapeHtml` helper function provides a reusable, consistent approach to XSS prevention
- This approach requires discipline to call the function consistently on all user-provided data
- The existing NameValidator significantly reduces actual risk, but output encoding is still best practice
- The helper function is defined once and can be used throughout the interop file
