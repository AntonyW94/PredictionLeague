namespace ThePredictions.SchemaCheck;

/// <summary>
/// A constructor Dapper may consider. <see cref="AccessibilityRank"/> mirrors Dapper's own ordering in
/// DefaultTypeMap.FindConstructor: public constructors are tried first, then internal/protected, then
/// private, and within each group by ascending parameter count.
/// </summary>
public sealed record ConstructorShape(int AccessibilityRank, IReadOnlyList<ParameterShape> Parameters)
{
    public const int PublicRank = 0;
    public const int InternalRank = 1;
    public const int PrivateRank = 2;
}
