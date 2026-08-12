namespace ThePredictions.SchemaCheck;

/// <summary>
/// Columns that allow null in the database but whose result types deny it, deliberately and for a stated reason.
/// </summary>
/// <remarks>
/// Every entry is a decision somebody has to be able to argue with, which is why the reason travels with it and is printed
/// on every run rather than kept in a commit message. An allowlist nobody sees is how a check stops meaning anything.
///
/// The bar for adding one: the database permits a state the product does not, and the honest fix is a constraint rather
/// than an annotation. Anything else - a column that really can be empty - gets a nullable result type instead.
/// </remarks>
public static class NullabilityExceptions
{
    /// <remarks>
    /// Empty, and worth keeping that way. The one entry it held - <c>AspNetUsers.Email</c>, nullable because Identity's
    /// schema is generic rather than because an account can be without a login - became migration
    /// <c>0008_AspNetUsersEmailRequired</c> instead. A constraint says it once; an exception says it for ever.
    /// </remarks>
    private static readonly Dictionary<string, string> ByColumnName = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsAllowed(string columnName) => ByColumnName.ContainsKey(columnName);

    public static IReadOnlyDictionary<string, string> All => ByColumnName;
}
