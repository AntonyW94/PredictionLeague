namespace ThePredictions.SchemaCheck;

/// <summary>
/// One Dapper read found in the source: where it is, what it materialises into, the SQL it runs and the
/// parameters that SQL needs. <see cref="SkipReason"/> is set when the tool cannot check it.
/// </summary>
public sealed record ReadCallSite(
    string File,
    int Line,
    string Method,
    string TypeArgument,
    IReadOnlyList<string>? TupleElementTypes,
    string? Sql,
    IReadOnlyList<InferredParameter> Parameters,
    string? SkipReason)
{
    public string Location => $"{File}:{Line}";
}
