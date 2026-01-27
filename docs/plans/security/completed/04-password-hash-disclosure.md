# Fix 04: Password Hash Information Disclosure

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P1 - High** - Fix this sprint

## Severity
**High** - Password hashes exposed to admin interface

## CWE Reference
[CWE-200: Exposure of Sensitive Information to an Unauthorized Actor](https://cwe.mitre.org/data/definitions/200.html)

---

## Problem Description

The `UserDto` includes a `PasswordHash` property which is populated by the `GetAllUsersQueryHandler` and returned to admin endpoints. Even though this is an admin-only endpoint, password hashes should never leave the database layer. Exposed hashes enable offline brute-force attacks.

### Affected Files

| File | Lines | Issue |
|------|-------|-------|
| `PredictionLeague.Contracts/Admin/Users/UserDto.cs` | 9 | DTO contains PasswordHash property |
| `PredictionLeague.Application/Features/Admin/Users/Queries/GetAllUsersQueryHandler.cs` | 26, 47 | SQL selects and maps PasswordHash |

### Current Vulnerable Code

**UserDto.cs**
```csharp
public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAdmin,
    string? PasswordHash,  // VULNERABLE: Should not be in DTO
    List<string> SocialProviders
);
```

**GetAllUsersQueryHandler.cs (Line 26)**
```csharp
const string sql = @"
    SELECT
        u.[Id],
        u.[FirstName] + ' ' + u.[LastName] AS FullName,
        u.[Email],
        u.[PhoneNumber],
        u.[PasswordHash],  // VULNERABLE: Selected from database
        CAST(CASE WHEN ar.[Id] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsAdmin,
        STRING_AGG(ul.[LoginProvider], ',') AS SocialProviders
    FROM [AspNetUsers] u
    ...";
```

**GetAllUsersQueryHandler.cs (Line 47)**
```csharp
return queryResult.Select(u => new UserDto(
    u.Id,
    u.FullName,
    u.Email,
    u.PhoneNumber,
    u.IsAdmin,
    u.PasswordHash,  // VULNERABLE: Mapped to DTO
    u.SocialProviders?.Split(',').ToList() ?? new List<string>()
));
```

### Risk

1. Admin compromises their account or session is hijacked
2. Attacker accesses admin user list endpoint
3. Attacker obtains password hashes for all users
4. Attacker performs offline brute-force attacks
5. Compromised passwords can be used across other sites (password reuse)

---

## Solution

### Step 1: Remove PasswordHash from UserDto

**File**: `PredictionLeague.Contracts/Admin/Users/UserDto.cs`

```csharp
public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAdmin,
    List<string> SocialProviders
);
```

### Step 2: Update Query Handler

Remove PasswordHash from SQL query and mapping.

**File**: `PredictionLeague.Application/Features/Admin/Users/Queries/GetAllUsersQueryHandler.cs`

```csharp
public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IEnumerable<UserDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetAllUsersQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<UserDto>> Handle(
        GetAllUsersQuery request,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                u.[Id],
                u.[FirstName] + ' ' + u.[LastName] AS FullName,
                u.[Email],
                u.[PhoneNumber],
                CAST(CASE WHEN ar.[Id] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS IsAdmin,
                STRING_AGG(ul.[LoginProvider], ',') AS SocialProviders
            FROM [AspNetUsers] u
            LEFT JOIN [AspNetUserRoles] aur ON u.[Id] = aur.[UserId]
            LEFT JOIN [AspNetRoles] ar ON aur.[RoleId] = ar.[Id] AND ar.[Name] = @AdminRoleName
            LEFT JOIN [AspNetUserLogins] ul ON u.[Id] = ul.[UserId]
            GROUP BY u.[Id], u.[FirstName], u.[LastName], u.[Email], u.[PhoneNumber], ar.[Id]
            ORDER BY u.[FirstName], u.[LastName];";

        var queryResult = await _dbConnection.QueryAsync<UserQueryResult>(
            sql,
            cancellationToken,
            new { AdminRoleName = ApplicationRoles.Administrator });

        return queryResult.Select(u => new UserDto(
            u.Id,
            u.FullName,
            u.Email,
            u.PhoneNumber,
            u.IsAdmin,
            u.SocialProviders?.Split(',').ToList() ?? new List<string>()
        ));
    }

    // Update internal class to remove PasswordHash
    private class UserQueryResult
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool IsAdmin { get; set; }
        public string? SocialProviders { get; set; }
    }
}
```

### Step 3: Check for Other Password Hash Exposure

Search the codebase for any other places that might expose password hashes:

```bash
grep -r "PasswordHash" --include="*.cs" .
```

Review and remove any other occurrences.

---

## If Admin Needs to Know "Has Password Set"

If the admin UI needs to know whether a user has a local password (vs. social-only login), add a boolean flag instead:

```csharp
public record UserDto(
    string Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsAdmin,
    bool HasLocalPassword,  // Safe alternative
    List<string> SocialProviders
);

// In SQL:
CAST(CASE WHEN u.[PasswordHash] IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasLocalPassword
```

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

```bash
# After deployment, verify no password hashes in response
curl -X GET "https://predictionleague.com/api/admin/users" \
  -H "Authorization: Bearer <admin-token>" \
  | jq '.[] | has("passwordHash")'
# Should return all false
```

1. Call admin users endpoint
2. Verify response JSON does not contain `passwordHash` field
3. Verify existing functionality still works

### Future: Unit Tests

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
[Fact]
public async Task Handle_DoesNotReturnPasswordHash()
{
    // Arrange & Act
    var result = await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

    // Assert - UserDto should not have PasswordHash property
    var userDto = result.First();
    var properties = typeof(UserDto).GetProperties();
    Assert.DoesNotContain(properties, p => p.Name == "PasswordHash");
}
```

</details>

---

## Checklist

- [ ] Remove `PasswordHash` from `UserDto`
- [ ] Remove `PasswordHash` from SQL query
- [ ] Remove `PasswordHash` from `UserQueryResult`
- [ ] Update mapping to not include `PasswordHash`
- [ ] Search codebase for other `PasswordHash` exposures
- [ ] Add `HasLocalPassword` if needed by admin UI
- [ ] Manual testing complete
- [ ] Code review approved
- [ ] Deployed to production
- [ ] Verify response doesn't contain password hashes

### Future (when test projects added)
- [ ] Write unit tests
- [ ] Write integration tests
