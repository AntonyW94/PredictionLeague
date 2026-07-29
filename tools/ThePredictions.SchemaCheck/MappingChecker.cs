namespace ThePredictions.SchemaCheck;

/// <summary>
/// Decides how Dapper will fill a result type and whether the described result set actually fits.
///
/// This mirrors DefaultTypeMap.FindConstructor: constructors are tried public-first then by ascending
/// parameter count, a parameterless one wins as soon as it is reached (which switches the type to
/// name-based property mapping), and a parameterised one only matches when every parameter lines up with
/// the column in the same position by name and type. If neither happens, materialisation throws.
/// </summary>
public static class MappingChecker
{
    public static CheckResult Check(ReadCallSite callSite, TypeIndex typeIndex, IReadOnlyList<ResultColumn> columns)
    {
        if (callSite.TupleElementTypes is { } elements)
            return CheckTuple(callSite, elements, columns);

        var shape = typeIndex.Resolve(callSite.TypeArgument, callSite.File, out var reason);
        if (shape is null)
            return new CheckResult(callSite, CheckStatus.Skipped, reason ?? "result type could not be resolved");

        var ordered = shape.Constructors
            .OrderBy(c => c.AccessibilityRank)
            .ThenBy(c => c.Parameters.Count)
            .ToList();

        foreach (var constructor in ordered)
        {
            if (constructor.Parameters.Count == 0)
                return CheckNameMapped(callSite, shape, typeIndex, columns);

            var problems = PositionalProblems(constructor, columns);
            if (problems.Count == 0)
                return new CheckResult(callSite, CheckStatus.Ok, $"positional match on the {constructor.Parameters.Count}-parameter constructor");
        }

        if (ordered.Count == 0)
            return new CheckResult(callSite, CheckStatus.Skipped, "no constructors found in the source declaration");

        // Nothing matched and there is no parameterless fallback: this is the failure Dapper reports as
        // "A parameterless default constructor or one matching signature ... is required".
        var closest = ordered
            .OrderBy(c => Math.Abs(c.Parameters.Count - columns.Count))
            .First();

        var detail = string.Join("; ", PositionalProblems(closest, columns).Take(4));

        return new CheckResult(callSite, CheckStatus.Broken,
            $"no constructor can be matched ({string.Join("/", ordered.Select(c => c.Parameters.Count))} parameters available, {columns.Count} columns); closest: {detail}");
    }

    private static CheckResult CheckTuple(ReadCallSite callSite, IReadOnlyList<string> elements, IReadOnlyList<ResultColumn> columns)
    {
        // Tuple element names are compile-time only, so Dapper matches these by position and type alone.
        if (elements.Count != columns.Count)
            return new CheckResult(callSite, CheckStatus.Mismatch, $"tuple has {elements.Count} elements but the result set has {columns.Count} columns");

        var problems = new List<string>();
        for (var i = 0; i < elements.Count; i++)
        {
            if (!TypesCompatible(elements[i], columns[i]))
                problems.Add($"pos {i + 1}: element is {elements[i]} but column {columns[i].Name} is {columns[i].SqlType}");
        }

        return problems.Count == 0
            ? new CheckResult(callSite, CheckStatus.Ok, "tuple matches by position")
            : new CheckResult(callSite, CheckStatus.Mismatch, string.Join("; ", problems));
    }

    private static CheckResult CheckNameMapped(ReadCallSite callSite, TypeShape shape, TypeIndex typeIndex, IReadOnlyList<ResultColumn> columns)
    {
        var (settable, readOnly, unresolvedBaseType) = typeIndex.ResolveMembers(shape);

        if (unresolvedBaseType is not null)
            return new CheckResult(callSite, CheckStatus.Skipped,
                $"name-mapped, but base type '{unresolvedBaseType}' is outside the scanned source so its members cannot be checked");

        var settableNames = settable.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var columnNames = columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dropped = columns.Where(c => !settableNames.Contains(c.Name)).Select(c => c.Name).ToList();
        var unfilled = settable.Where(m => !columnNames.Contains(m.Name)).Select(m => m.Name).ToList();

        if (dropped.Count == 0 && unfilled.Count == 0)
            return new CheckResult(callSite, CheckStatus.Ok, $"name-mapped: all {columns.Count} columns map to a settable member");

        var parts = new List<string>();

        if (dropped.Count > 0)
        {
            var shadowed = dropped.Where(readOnly.Contains).ToList();
            var note = shadowed.Count > 0 ? $" (no setter: {string.Join(", ", shadowed)})" : string.Empty;
            parts.Add($"columns discarded: {string.Join(", ", dropped)}{note}");
        }

        if (unfilled.Count > 0)
            parts.Add($"members left at their default: {string.Join(", ", unfilled)}");

        return new CheckResult(callSite, CheckStatus.SilentDrop, string.Join("; ", parts));
    }

    private static List<string> PositionalProblems(ConstructorShape constructor, IReadOnlyList<ResultColumn> columns)
    {
        var problems = new List<string>();

        if (constructor.Parameters.Count != columns.Count)
            problems.Add($"constructor takes {constructor.Parameters.Count} parameters but the result set has {columns.Count} columns");

        var limit = Math.Min(constructor.Parameters.Count, columns.Count);

        for (var i = 0; i < limit; i++)
        {
            var parameter = constructor.Parameters[i];
            var column = columns[i];

            if (!string.Equals(parameter.Name, column.Name, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"pos {i + 1}: parameter '{parameter.Name}' vs column '{column.Name}'");
                continue;
            }

            if (!TypesCompatible(parameter.Type, column))
                problems.Add($"pos {i + 1}: '{parameter.Name}' is {parameter.Type} but column is {column.SqlType} ({column.ClrType})");
        }

        return problems;
    }

    private static bool TypesCompatible(string sourceType, ResultColumn column)
    {
        var normalised = SqlTypeMap.NormaliseClrType(sourceType);

        if (string.Equals(normalised, column.ClrType, StringComparison.Ordinal))
            return true;

        if (normalised is "object" or "dynamic")
            return true;

        if (normalised == "char" && column.ClrType == "string")
            return true;

        // An unrecognised type is an enum or a type with a registered handler. Dapper accepts an enum
        // against its underlying integral type or against a string, and a handled type against anything.
        if (!SqlTypeMap.IsKnownClrType(normalised))
            return column.ClrType is "int" or "byte" or "short" or "long" or "string";

        return false;
    }
}
