# P2: Entry Code Character Validation Missing

## Summary

**Severity:** P2 - Medium
**Type:** Input Validation Gap
**CWE:** CWE-20 (Improper Input Validation)

## Description

The `JoinLeagueRequestValidator` only validates that entry codes are exactly 6 characters long, but doesn't enforce alphanumeric-only characters as specified in `CLAUDE.md`.

## Affected Files

- `PredictionLeague.Validators/Leagues/JoinLeagueRequestValidator.cs` (lines 12-14)

## Current Code

```csharp
RuleFor(x => x.EntryCode)
    .NotEmpty().WithMessage("Please enter an entry code.")
    .Length(6).WithMessage("The entry code must be 6 characters long.");
    // Missing: Alphanumeric validation
```

## Problem

Entry codes can contain:
- Special characters: `@#$%^&`
- Unicode/Cyrillic: `АБВГДЕ` (looks like Latin but isn't)
- Spaces: `AB CD  `
- Injection attempts: `<script`

## Exploitation Scenario

While entry codes are used for database lookup (parameterised, so no SQL injection), malformed codes could:
1. Cause unexpected behaviour in code comparisons
2. Allow homograph attacks (Cyrillic А looking like Latin A)
3. Confuse logging and monitoring systems
4. Bypass input validation for other features that trust the code format

## Recommended Fix

Update `JoinLeagueRequestValidator.cs`:

```csharp
public JoinLeagueRequestValidator()
{
    RuleFor(x => x.EntryCode)
        .NotEmpty().WithMessage("Please enter an entry code.")
        .Length(6).WithMessage("The entry code must be 6 characters long.")
        .Matches(@"^[A-Za-z0-9]{6}$")
            .WithMessage("Entry code must contain only letters and numbers.");
}
```

### Alternative: Case-Insensitive Uppercase Only

If entry codes should be case-insensitive:

```csharp
RuleFor(x => x.EntryCode)
    .NotEmpty().WithMessage("Please enter an entry code.")
    .Length(6).WithMessage("The entry code must be 6 characters long.")
    .Matches(@"^[A-Z0-9]{6}$", RegexOptions.IgnoreCase)
        .WithMessage("Entry code must contain only letters and numbers.");
```

## Also Check Entry Code Generation

Verify that entry code generation only produces alphanumeric codes:

```csharp
// League.cs or wherever codes are generated
private static string GenerateEntryCode()
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    // ... generation logic
}
```

## Testing

1. Test valid codes: `ABC123`, `abc123`, `ABCDEF`
2. Test invalid codes:
   - `AB@#$%` → should fail
   - `AB CD  ` → should fail
   - `АБВГДЕ` → should fail (Cyrillic)
   - `<scrip` → should fail
3. Verify error message is user-friendly

## References

- [OWASP Input Validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
- [Unicode Homograph Attacks](https://en.wikipedia.org/wiki/IDN_homograph_attack)
