# Checklist: Creating a New Command

Use this checklist when creating a new command (write operation).

## Files to Create

- [ ] `src/PredictionLeague.Application/Features/{Area}/Commands/{CommandName}/{CommandName}Command.cs`
- [ ] `src/PredictionLeague.Application/Features/{Area}/Commands/{CommandName}/{CommandName}CommandHandler.cs`
- [ ] `src/PredictionLeague.Validators/{Area}/{CommandName}CommandValidator.cs`
- [ ] `src/PredictionLeague.Contracts/{Area}/{ResultDto}.cs` (if returning data)

## Command Definition

```csharp
// {CommandName}Command.cs
public record {CommandName}Command(
    // Properties in PascalCase
    string Name,
    int SeasonId,
    string UserId) : IRequest<{ResultType}>;

// For transactional operations, also implement ITransactionalRequest:
public record {CommandName}Command(...) : IRequest<{ResultType}>, ITransactionalRequest;
```

## Handler Implementation

```csharp
// {CommandName}CommandHandler.cs
public class {CommandName}CommandHandler : IRequestHandler<{CommandName}Command, {ResultType}>
{
    private readonly I{Entity}Repository _{entity}Repository;
    private readonly ILogger<{CommandName}CommandHandler> _logger;

    public {CommandName}CommandHandler(
        I{Entity}Repository {entity}Repository,
        ILogger<{CommandName}CommandHandler> logger)
    {
        _{entity}Repository = {entity}Repository;
        _logger = logger;
    }

    public async Task<{ResultType}> Handle({CommandName}Command request, CancellationToken ct)
    {
        // 1. Use factory method for new entities
        var entity = Entity.Create(request.Property1, request.Property2);

        // 2. Use repository for persistence
        var created = await _{entity}Repository.CreateAsync(entity, ct);

        // 3. Log with correct format
        _logger.LogInformation("Entity (ID: {EntityId}) created by User (ID: {UserId})",
            created.Id, request.UserId);

        // 4. Return DTO, not domain model
        return new ResultDto(created.Id, created.Name);
    }
}
```

## Validator

```csharp
// {CommandName}CommandValidator.cs
public class {CommandName}CommandValidator : AbstractValidator<{CommandName}Command>
{
    public {CommandName}CommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.SeasonId)
            .GreaterThan(0);
    }
}
```

## Verification Checklist

- [ ] Command record uses PascalCase property names
- [ ] Handler uses repository (not IApplicationReadDbConnection)
- [ ] Handler uses `Entity.Create()` for new entities
- [ ] Handler returns new instance from repository (immutability)
- [ ] Logging uses `EntityName (ID: {EntityNameId})` format
- [ ] Validator exists with appropriate rules
- [ ] One public type per file
- [ ] All async methods end with `Async`
- [ ] UK English spelling used throughout

## Testing Checklist

- [ ] Unit tests written for any new domain logic triggered by the command
- [ ] Test naming follows `MethodName_ShouldX_WhenY()` convention
- [ ] Coverage report run (`tools\Test Coverage\coverage-unit.bat`) and confirms 100% line and branch coverage
- [ ] Any untestable code (e.g. ORM constructors) marked with `[ExcludeFromCodeCoverage]`
