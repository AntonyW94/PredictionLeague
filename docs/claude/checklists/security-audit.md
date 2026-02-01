# Checklist: Security Audit

Use this checklist when reviewing code for security vulnerabilities or before deploying new features.

## Authentication & Authorisation

### JWT Token Handling

- [ ] Access tokens have short expiry (15-30 minutes)
- [ ] Refresh tokens are rotated on use
- [ ] Token validation checks expiry, issuer, and audience
- [ ] Logout invalidates refresh tokens server-side

```csharp
// CORRECT - Validate all claims
services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero // No tolerance for expired tokens
        };
    });
```

### Blazor WebAssembly Token Storage

**IMPORTANT:** This project uses Blazor WebAssembly. HttpOnly cookies do NOT work for Blazor WASM authentication because:

1. Blazor WASM runs entirely in the browser (client-side)
2. JavaScript must access the token to attach it to API requests via `Authorization` header
3. HttpOnly cookies are inaccessible to JavaScript by design

**This project's auth flow:**
- Tokens stored in `localStorage`
- Passed via `Authorization: Bearer {token}` header
- `ApiAuthenticationStateProvider` manages token lifecycle

```csharp
// This project's pattern - tokens via Authorization header
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", accessToken);
```

**DO NOT** attempt to "fix" this by moving tokens to HttpOnly cookies - it will break the authentication flow.

### Endpoint Authorisation

- [ ] All endpoints have explicit `[Authorize]` or `[AllowAnonymous]`
- [ ] Sensitive operations check resource ownership
- [ ] Admin endpoints use role-based authorisation
- [ ] No authorisation logic in views/components (server-side only)

```csharp
// CORRECT - Check resource ownership
public async Task<LeagueDto> Handle(UpdateLeagueCommand request, CancellationToken ct)
{
    var league = await _leagueRepository.GetByIdAsync(request.LeagueId, ct);

    if (league is null)
    {
        throw new NotFoundException("League", request.LeagueId);
    }

    // Verify the user owns this resource
    if (league.AdministratorUserId != request.UserId)
    {
        throw new ForbiddenException("You do not have permission to modify this league");
    }

    // Proceed with update...
}
```

## Input Validation

### SQL Injection Prevention

- [ ] All database queries use parameterised queries
- [ ] No string concatenation in SQL
- [ ] No dynamic table/column names from user input

```csharp
// CORRECT - Parameterised query
const string sql = @"
    SELECT [Id], [Name]
    FROM [Leagues]
    WHERE [Id] = @LeagueId AND [AdministratorUserId] = @UserId";

await connection.QueryAsync<League>(sql, new { LeagueId = id, UserId = userId });

// WRONG - SQL injection vulnerability
var sql = $"SELECT * FROM Leagues WHERE Id = {id}";  // NEVER DO THIS
```

### Cross-Site Scripting (XSS) Prevention

- [ ] User input is encoded before rendering
- [ ] Blazor's default encoding is not bypassed
- [ ] `@((MarkupString)...)` only used with sanitised content
- [ ] Content-Security-Policy headers configured

```razor
@* CORRECT - Blazor auto-encodes *@
<p>@user.Name</p>

@* DANGEROUS - Only use with sanitised content *@
<div>@((MarkupString)sanitisedHtml)</div>

@* WRONG - Never render unsanitised user HTML *@
<div>@((MarkupString)user.Biography)</div>
```

### Request Validation

- [ ] All commands/queries have FluentValidation validators
- [ ] String lengths are limited
- [ ] Numeric ranges are validated
- [ ] File uploads validate type, size, and content
- [ ] URLs are validated if stored/used

```csharp
public class CreateLeagueCommandValidator : AbstractValidator<CreateLeagueCommand>
{
    public CreateLeagueCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)  // Prevent oversized input
            .Matches(@"^[\w\s\-]+$").WithMessage("Name contains invalid characters");

        RuleFor(x => x.SeasonId)
            .GreaterThan(0)
            .LessThan(100000);  // Reasonable upper bound
    }
}
```

## Data Protection

### Sensitive Data Handling

- [ ] Passwords are hashed with strong algorithm (bcrypt, Argon2)
- [ ] Password hashes use unique salts
- [ ] PII is encrypted at rest where required
- [ ] Sensitive data not logged (passwords, tokens, PII)
- [ ] API responses don't expose internal IDs unnecessarily

```csharp
// WRONG - Logging sensitive data
_logger.LogInformation("User (Email: {Email}) logged in with password {Password}", email, password);

// CORRECT - Never log passwords or tokens
_logger.LogInformation("User (Email: {Email}) logged in successfully", email);
```

### Connection Strings & Secrets

- [ ] No secrets in `appsettings.json` (use KeyVault references)
- [ ] No secrets in source code
- [ ] Connection strings use managed identity where possible
- [ ] Development secrets use User Secrets or environment variables

```json
// CORRECT - KeyVault reference
{
  "ConnectionStrings": {
    "DefaultConnection": "@Microsoft.KeyVault(VaultName=myvault;SecretName=DbConnection)"
  }
}

// WRONG - Hardcoded secret
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Password=MyP@ssw0rd123"
  }
}
```

## API Security

### Rate Limiting

- [ ] Authentication endpoints have strict rate limits
- [ ] API endpoints have reasonable rate limits
- [ ] Rate limit responses include `Retry-After` header

### CORS Configuration

- [ ] CORS origins are explicitly listed (not `*`)
- [ ] CORS methods are restricted to those needed
- [ ] Credentials mode is only enabled when necessary

```csharp
// CORRECT - Specific origins
services.AddCors(options =>
{
    options.AddPolicy("Production", builder =>
    {
        builder
            .WithOrigins("https://predictionleague.com")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Authorization", "Content-Type");
    });
});

// WRONG - Allow all origins
builder.AllowAnyOrigin();  // NEVER in production
```

### HTTP Security Headers

- [ ] `Strict-Transport-Security` (HSTS) enabled
- [ ] `X-Content-Type-Options: nosniff`
- [ ] `X-Frame-Options: DENY` or `SAMEORIGIN`
- [ ] `Content-Security-Policy` configured
- [ ] `X-XSS-Protection: 1; mode=block`

## Error Handling

### Information Disclosure

- [ ] Stack traces not exposed in production
- [ ] Database errors not exposed to users
- [ ] Internal paths not exposed in errors
- [ ] Enumeration attacks prevented (consistent error messages)

```csharp
// WRONG - Reveals whether email exists
if (user is null)
{
    return BadRequest("User with this email does not exist");
}
if (!ValidatePassword(user, password))
{
    return BadRequest("Incorrect password");
}

// CORRECT - Consistent message prevents enumeration
if (user is null || !ValidatePassword(user, password))
{
    return BadRequest("Invalid email or password");
}
```

### Exception Handling

- [ ] Global exception handler catches unhandled exceptions
- [ ] Exceptions are logged with context
- [ ] Users receive generic error messages
- [ ] Sensitive exception details not returned

## Session Management

### Cookie Security (Server-Rendered Apps Only)

> **NOT APPLICABLE TO THIS PROJECT**
>
> This project uses Blazor WebAssembly with JWT tokens stored in localStorage and passed via Authorization header. The cookie security section below is for reference only and does NOT apply to this project's authentication flow.
>
> See "Blazor WebAssembly Token Storage" section above for this project's auth pattern.

For server-rendered applications (Blazor Server, MVC, Razor Pages) that use cookie authentication:

- [ ] Authentication cookies are `HttpOnly`
- [ ] Authentication cookies are `Secure` (HTTPS only)
- [ ] `SameSite` attribute set appropriately
- [ ] Cookie expiry is reasonable

```csharp
// FOR SERVER-RENDERED APPS ONLY - Not used in this project
services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
});
```

## Dependency Security

### Package Vulnerabilities

- [ ] Run `dotnet list package --vulnerable`
- [ ] No packages with known high/critical vulnerabilities
- [ ] Packages are reasonably up to date
- [ ] Unused packages removed

```bash
# Check for vulnerable packages
dotnet list package --vulnerable --include-transitive
```

## Audit Logging

### Security Events to Log

- [ ] Login attempts (success and failure)
- [ ] Password changes
- [ ] Permission changes
- [ ] Access to sensitive data
- [ ] Admin actions
- [ ] Token refresh/revocation

```csharp
// Log security-relevant events
_logger.LogInformation(
    "User (ID: {UserId}) logged in from IP (Address: {IpAddress})",
    userId, ipAddress);

_logger.LogWarning(
    "Failed login attempt for User (Email: {Email}) from IP (Address: {IpAddress})",
    email, ipAddress);

_logger.LogInformation(
    "User (ID: {UserId}) changed password",
    userId);
```

## Quick Security Checklist

For quick reviews, check these critical items:

| Area | Check |
|------|-------|
| Auth | All endpoints have `[Authorize]` or `[AllowAnonymous]` |
| Auth | Resource ownership verified before modifications |
| SQL | All queries use parameters, no string concatenation |
| Input | All user input validated with length limits |
| Secrets | No secrets in code or config files |
| Errors | Stack traces not exposed in production |
| Logging | No passwords, tokens, or PII logged |
| CORS | Origins explicitly listed, not `*` |

## Running a Security Audit

1. **Review authentication flow**
   - Check token handling
   - Verify refresh token rotation
   - Test logout invalidation

2. **Review all API endpoints**
   - Check authorisation attributes
   - Verify ownership checks
   - Test with different user roles

3. **Review database queries**
   - Search for string interpolation in SQL
   - Verify all parameters are sanitised

4. **Review error handling**
   - Test with invalid inputs
   - Check error responses don't leak info

5. **Check dependencies**
   - Run vulnerability scan
   - Update outdated packages

6. **Review configuration**
   - Check for hardcoded secrets
   - Verify security headers
