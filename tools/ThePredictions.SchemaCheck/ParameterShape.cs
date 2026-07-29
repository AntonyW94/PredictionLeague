namespace ThePredictions.SchemaCheck;

/// <summary>
/// One parameter of a constructor Dapper might have to match, as written in the source.
/// </summary>
public sealed record ParameterShape(string Type, string Name);
