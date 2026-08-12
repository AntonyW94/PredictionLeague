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
    private static readonly Dictionary<string, string> ByColumnName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Email"] =
            "AspNetUsers.Email is nullable because ASP.NET Identity's schema is generic, not because an account can be " +
            "without one - it is the login, and no row on dev is null - prod has not been checked. Declaring the result types " +
            "nullable would add a 'skip anybody with no address' branch to nine reads for a state the product cannot " +
            "reach, and a test for each. The fix is a NOT NULL constraint; until that migration lands, this is stated " +
            "here rather than worked around nine times."
    };

    public static bool IsAllowed(string columnName) => ByColumnName.ContainsKey(columnName);

    public static IReadOnlyDictionary<string, string> All => ByColumnName;
}
