# Task: Project Setup

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create the `ThePredictions.Domain.Tests.Unit` project with all required package references and add it to the solution.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/ThePredictions.Domain.Tests.Unit.csproj` | Create | Test project file with package references |
| `ThePredictions.sln` | Modify | Add test project to solution |

## Implementation Steps

### Step 1: Create the test project directory

Create the folder structure:

```
tests/
└── Unit/
    └── ThePredictions.Domain.Tests.Unit/
        ├── ThePredictions.Domain.Tests.Unit.csproj
        ├── Helpers/
        ├── Models/
        └── Services/
```

### Step 2: Create the project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\ThePredictions.Domain\ThePredictions.Domain.csproj" />
  </ItemGroup>
</Project>
```

### Step 3: Add to solution

```bash
dotnet sln ThePredictions.sln add tests/Unit/ThePredictions.Domain.Tests.Unit/ThePredictions.Domain.Tests.Unit.csproj --solution-folder Tests/Unit
```

### Step 4: Create the FakeDateTimeProvider helper

Create `tests/Unit/ThePredictions.Domain.Tests.Unit/Helpers/FakeDateTimeProvider.cs`:

```csharp
using PredictionLeague.Domain.Common;

namespace ThePredictions.Domain.Tests.Unit.Helpers;

public class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}
```

This is a hand-rolled fake (not a mock) that all test classes will use. The settable `UtcNow` property allows tests to advance time mid-test.

> **Prerequisite:** Task 2 (`IDateTimeProvider`) must be completed first so the interface exists.

### Step 5: Verify the project builds

```bash
dotnet build tests/Unit/ThePredictions.Domain.Tests.Unit/ThePredictions.Domain.Tests.Unit.csproj
```

## Verification

- [ ] Project file created with correct package references
- [ ] Project added to solution file under `Tests/Unit` solution folder
- [ ] `FakeDateTimeProvider` created in `Helpers/` folder
- [ ] `dotnet build` succeeds with no errors
- [ ] `dotnet test` runs (with 0 tests initially)

## Notes

- No mocking library needed — a hand-rolled `FakeDateTimeProvider` is used instead. NSubstitute is only needed in the Application.Tests.Unit project.
- `FakeDateTimeProvider` requires task 2 (`IDateTimeProvider`) to be completed first.
- Check the latest stable package versions before installing — the versions listed above are from the test suite plan and may have newer releases.
