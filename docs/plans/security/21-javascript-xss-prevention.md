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

### Option 1: Use textContent for Safe Text Insertion (Recommended)

**File:** `PredictionLeague.Web.Client/wwwroot/js/interop.js`

**Replace template literal HTML with DOM manipulation:**

```javascript
// showReassignLeagueConfirm function
showReassignLeagueConfirm: function (message, userList, userToDeleteId) {
    return new Promise((resolve) => {
        // Create select element safely
        const selectContainer = document.createElement('div');
        const select = document.createElement('select');
        select.id = 'swal-reassign-select';
        select.className = 'swal2-select';

        // Add options safely using textContent (no HTML injection possible)
        userList
            .filter(user => user.id !== userToDeleteId)
            .forEach(user => {
                const option = document.createElement('option');
                option.value = user.id;
                option.textContent = user.fullName;  // SAFE: textContent escapes HTML
                select.appendChild(option);
            });

        selectContainer.appendChild(select);

        Swal.fire({
            title: message,  // Already safe - comes from C# string literal
            html: selectContainer,  // Now safe - built with DOM methods
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

// showRoleChangeConfirm function
showRoleChangeConfirm: function (userName, currentRole) {
    return new Promise((resolve) => {
        // Create title element safely
        const titleElement = document.createElement('span');
        titleElement.textContent = `Change role for ${userName}`;  // SAFE

        Swal.fire({
            title: titleElement,  // Pass DOM element instead of string
            text: `Current role: ${currentRole}`,
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

---

### Option 2: Use a Sanitisation Function

If template literals must be used, add a sanitisation helper:

```javascript
// Add at top of interop.js
const blazorInterop = {
    // Helper function to escape HTML entities
    escapeHtml: function(unsafe) {
        if (typeof unsafe !== 'string') return '';
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    },

    showReassignLeagueConfirm: function (message, userList, userToDeleteId) {
        return new Promise((resolve) => {
            const optionsHtml = userList
                .filter(user => user.id !== userToDeleteId)
                .map(user => `<option value="${this.escapeHtml(user.id)}">${this.escapeHtml(user.fullName)}</option>`)
                .join('');

            Swal.fire({
                title: this.escapeHtml(message),
                html: `<select id="swal-reassign-select" class="swal2-select">${optionsHtml}</select>`,
                // ... rest of config
            });
        });
    },

    showRoleChangeConfirm: function (userName, currentRole) {
        return new Promise((resolve) => {
            Swal.fire({
                title: `Change role for ${this.escapeHtml(userName)}`,
                text: `Current role: ${this.escapeHtml(currentRole)}`,
                // ... rest of config
            });
        });
    }
};
```

---

### Option 3: Use DOMPurify Library

For comprehensive HTML sanitisation, add DOMPurify:

**File:** `PredictionLeague.Web.Client/wwwroot/index.html`

```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/dompurify/3.0.6/purify.min.js"></script>
```

**File:** `interop.js`

```javascript
showReassignLeagueConfirm: function (message, userList, userToDeleteId) {
    const optionsHtml = userList
        .filter(user => user.id !== userToDeleteId)
        .map(user => `<option value="${DOMPurify.sanitize(user.id)}">${DOMPurify.sanitize(user.fullName)}</option>`)
        .join('');

    const safeHtml = DOMPurify.sanitize(
        `<select id="swal-reassign-select" class="swal2-select">${optionsHtml}</select>`
    );

    Swal.fire({
        title: DOMPurify.sanitize(message),
        html: safeHtml,
        // ...
    });
}
```

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

If the DOM manipulation approach causes issues:
1. Revert to template literals with `escapeHtml` function
2. Ensure `escapeHtml` is called on all user data

---

## Notes

- Option 1 (DOM manipulation) is recommended as it's the safest approach
- Option 2 (escapeHtml) is simpler but requires discipline to call consistently
- Option 3 (DOMPurify) is most comprehensive but adds external dependency
- The existing NameValidator significantly reduces actual risk, but output encoding is still best practice
