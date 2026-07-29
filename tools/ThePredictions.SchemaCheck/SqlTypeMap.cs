namespace ThePredictions.SchemaCheck;

/// <summary>
/// Maps SQL Server types to the CLR types Dapper's reader produces, and normalises the way source code
/// spells CLR types so the two can be compared.
/// </summary>
public static class SqlTypeMap
{
    private static readonly Dictionary<string, string> SqlToClr = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bigint"] = "long",
        ["int"] = "int",
        ["smallint"] = "short",
        ["tinyint"] = "byte",
        ["bit"] = "bool",
        ["money"] = "decimal",
        ["smallmoney"] = "decimal",
        ["float"] = "double",
        ["real"] = "float",
        ["date"] = "DateTime",
        ["datetime"] = "DateTime",
        ["datetime2"] = "DateTime",
        ["smalldatetime"] = "DateTime",
        ["datetimeoffset"] = "DateTimeOffset",
        ["time"] = "TimeSpan",
        ["uniqueidentifier"] = "Guid",
        ["xml"] = "string",
        ["sql_variant"] = "object"
    };

    private static readonly Dictionary<string, string> ClrAliases = new(StringComparer.Ordinal)
    {
        ["Int16"] = "short",
        ["Int32"] = "int",
        ["Int64"] = "long",
        ["Byte"] = "byte",
        ["Boolean"] = "bool",
        ["String"] = "string",
        ["Decimal"] = "decimal",
        ["Double"] = "double",
        ["Single"] = "float",
        ["Char"] = "char",
        ["Object"] = "object"
    };

    private static readonly HashSet<string> KnownClrTypes = new(StringComparer.Ordinal)
    {
        "int", "long", "short", "byte", "bool", "string", "decimal", "double", "float", "char",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid", "byte[]", "object"
    };

    /// <summary>Converts a system_type_name such as "decimal(30,2)" into the CLR type Dapper returns.</summary>
    public static string ToClrType(string systemTypeName)
    {
        var bare = systemTypeName.Split('(')[0].Trim();

        if (bare.Equals("decimal", StringComparison.OrdinalIgnoreCase) || bare.Equals("numeric", StringComparison.OrdinalIgnoreCase))
            return "decimal";

        if (bare.EndsWith("varchar", StringComparison.OrdinalIgnoreCase) || bare.EndsWith("char", StringComparison.OrdinalIgnoreCase)
            || bare.Equals("text", StringComparison.OrdinalIgnoreCase) || bare.Equals("ntext", StringComparison.OrdinalIgnoreCase))
            return "string";

        if (bare.EndsWith("binary", StringComparison.OrdinalIgnoreCase) || bare.Equals("image", StringComparison.OrdinalIgnoreCase))
            return "byte[]";

        return SqlToClr.TryGetValue(bare, out var clr) ? clr : bare;
    }

    /// <summary>Strips nullability and namespace qualification so "System.Int32?" compares as "int".</summary>
    public static string NormaliseClrType(string sourceType)
    {
        var bare = sourceType.Trim().TrimEnd('?').Trim();

        var lastDot = bare.LastIndexOf('.');
        if (lastDot >= 0 && !bare.Contains('<', StringComparison.Ordinal))
            bare = bare[(lastDot + 1)..];

        return ClrAliases.TryGetValue(bare, out var alias) ? alias : bare;
    }

    /// <summary>True for the primitives Dapper matches exactly; anything else may be an enum or a handled type.</summary>
    public static bool IsKnownClrType(string normalisedType) => KnownClrTypes.Contains(normalisedType);

    public static bool IsScalar(string sourceType) => IsKnownClrType(NormaliseClrType(sourceType)) || NormaliseClrType(sourceType) == "dynamic";
}
