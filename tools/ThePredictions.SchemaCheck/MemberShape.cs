namespace ThePredictions.SchemaCheck;

/// <summary>
/// A property or field Dapper can assign by name. Private and protected setters count - Dapper writes
/// through them, which is how the domain entities hydrate.
/// </summary>
public sealed record MemberShape(string Name, string Type);
