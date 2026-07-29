namespace ThePredictions.SchemaCheck;

/// <summary>
/// Everything about a result type that decides how Dapper will fill it: the constructors it can match
/// positionally, and the members it can assign by name if it falls back to property mapping.
/// </summary>
public sealed record TypeShape(
    string Name,
    string File,
    IReadOnlyList<ConstructorShape> Constructors,
    IReadOnlyList<MemberShape> SettableMembers,
    IReadOnlyList<string> ReadOnlyMembers,
    string? BaseTypeName);
