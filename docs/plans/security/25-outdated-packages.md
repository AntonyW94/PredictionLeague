# P1: Outdated Security-Critical Packages

## Summary

**Severity:** P1 - High
**Type:** Vulnerable Dependencies
**CWE:** CWE-1104 (Use of Unmaintained Third Party Components)
**OWASP:** A06:2021 - Vulnerable and Outdated Components

## Description

Several security-critical NuGet packages are severely outdated, particularly authentication and identity packages from the .NET Core 2.1 era (~2018). These packages are missing years of security patches.

## Affected Packages

### Critical (Must Fix)

| Package | Current | Required | Location |
|---------|---------|----------|----------|
| Microsoft.AspNetCore.Identity | 2.3.1 | 8.0.x | Web.Client.csproj |
| Microsoft.AspNetCore.Authentication.Abstractions | 2.3.0 | 8.0.x | Application.csproj |

### High Priority

| Package | Current | Required | Location |
|---------|---------|----------|----------|
| System.IdentityModel.Tokens.Jwt | 7.5.0 | 8.0.x | Web.Client.csproj |
| System.IdentityModel.Tokens.Jwt | 7.7.1 | 8.0.x | Web.csproj |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 8.0.6 | 8.0.17 | Web.Client.csproj |

### Known Vulnerable (Acknowledged)

| Package | Current | Notes |
|---------|---------|-------|
| brevo_csharp | 1.1.1 | Known vulnerable, latest available |

## Security Impact

### Microsoft.AspNetCore.Identity 2.3.1

- Password hashing algorithms may be outdated
- Missing security patches from 6+ years of updates
- Potential authentication bypass vulnerabilities
- Incompatible with modern .NET 8 security features

### Microsoft.AspNetCore.Authentication.Abstractions 2.3.0

- Authentication pipeline vulnerabilities
- Missing claim validation improvements
- Potential token handling issues

### System.IdentityModel.Tokens.Jwt 7.x

- JWT validation vulnerabilities
- Missing algorithm restrictions
- Token parsing security improvements

## Recommended Fix

### Step 1: Update Critical Packages

Edit `PredictionLeague.Web.Client.csproj`:
```xml
<!-- Remove or update -->
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.1" />
<!-- Replace with -->
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="8.0.17" />
```

Edit `PredictionLeague.Application.csproj`:
```xml
<!-- Remove or update -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.Abstractions" Version="2.3.0" />
<!-- Replace with -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.Abstractions" Version="8.0.17" />
```

### Step 2: Update JWT Packages

Both Web projects:
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />
```

### Step 3: Update Dev Server

`PredictionLeague.Web.Client.csproj`:
```xml
<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="8.0.17" />
```

### Step 4: Run Commands

```bash
# Update packages
dotnet restore

# Build to verify no breaking changes
dotnet build PredictionLeague.sln

# Run tests (when available)
dotnet test
```

## Testing After Update

1. Verify login/logout functionality works
2. Verify JWT token generation and validation
3. Verify refresh token flow
4. Verify Google OAuth authentication
5. Test password hashing (register new user, login)
6. Verify admin role authorisation

## Breaking Changes to Watch

- Password hash format changes (existing users may need to re-authenticate)
- JWT claim structure changes
- Authentication middleware behaviour

## References

- [.NET 8 Security Updates](https://github.com/dotnet/aspnetcore/releases)
- [NuGet Vulnerability Database](https://github.com/advisories)
