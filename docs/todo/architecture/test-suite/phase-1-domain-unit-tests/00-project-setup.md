# Task: Project Setup

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create the `PredictionLeague.Domain.Tests` project with all required package references and add it to the solution.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/PredictionLeague.Domain.Tests/PredictionLeague.Domain.Tests.csproj` | Create | Test project file with package references |
| `PredictionLeague.sln` | Modify | Add test project to solution |

## Implementation Steps

### Step 1: Create the test project directory

Create the folder structure:

```
tests/
└── PredictionLeague.Domain.Tests/
    ├── PredictionLeague.Domain.Tests.csproj
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
    <PackageReference Include="NSubstitute" Version="5.3.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PredictionLeague.Domain\PredictionLeague.Domain.csproj" />
  </ItemGroup>
</Project>
```

NSubstitute is included for `IEntryCodeUniquenessChecker` used by `League.GenerateEntryCode()`.

### Step 3: Add to solution

```bash
dotnet sln PredictionLeague.sln add tests/PredictionLeague.Domain.Tests/PredictionLeague.Domain.Tests.csproj
```

### Step 4: Verify the project builds

```bash
dotnet build tests/PredictionLeague.Domain.Tests/PredictionLeague.Domain.Tests.csproj
```

## Verification

- [ ] Project file created with correct package references
- [ ] Project added to solution file
- [ ] `dotnet build` succeeds with no errors
- [ ] `dotnet test` runs (with 0 tests initially)

## Notes

- NSubstitute is only needed for `League.GenerateEntryCode()` tests where `IEntryCodeUniquenessChecker` must be mocked. All other domain tests are pure unit tests.
- Check the latest stable package versions before installing — the versions listed above are from the test suite plan and may have newer releases.
