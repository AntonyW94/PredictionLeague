# Housekeeping: Remove Legacy Package References

## Summary

**Priority:** Low (Housekeeping)
**Type:** Dependency Cleanup
**Risk:** Minimal - functionality provided by .NET 10 shared framework

## Description

Two legacy package references from the .NET Core 2.x era remain in the codebase. While they are at their latest available versions (2.3.9) and pose no security risk, they are unnecessary as their functionality is now included in the .NET 10 shared framework.

## Packages to Remove

| Package | Location | Reason |
|---------|----------|--------|
| `Microsoft.AspNetCore.Identity` | Web.Client.csproj | Provided by `Microsoft.AspNetCore.App` |
| `Microsoft.AspNetCore.Authentication.Abstractions` | Application.csproj | Provided by `Microsoft.AspNetCore.App` |

## Steps

### Step 1: Remove from Web.Client.csproj

Edit `PredictionLeague.Web.Client/PredictionLeague.Web.Client.csproj`:

```xml
<!-- Remove this line -->
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.9" />
```

### Step 2: Remove from Application.csproj

Edit `PredictionLeague.Application/PredictionLeague.Application.csproj`:

```xml
<!-- Remove this line -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.Abstractions" Version="2.3.9" />
```

### Step 3: Build and Test

```bash
# Restore and build
dotnet restore PredictionLeague.sln
dotnet build PredictionLeague.sln

# If build succeeds, test the application:
# 1. Run the application
# 2. Test login/logout
# 3. Test registration
# 4. Test Google OAuth (if applicable)
# 5. Verify JWT token handling works
```

## Expected Outcome

- Build should succeed without these packages
- All authentication functionality should work as before
- The types previously provided by these packages are now in the shared framework

## Rollback

If the build fails or authentication breaks, simply re-add the package references:

```xml
<!-- Web.Client.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Identity" Version="2.3.9" />

<!-- Application.csproj -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.Abstractions" Version="2.3.9" />
```

## Notes

- These packages were deprecated after .NET Core 2.x
- Version 2.3.9 is the final version ever released
- .NET 10 includes all the types from these packages in `Microsoft.AspNetCore.App`
