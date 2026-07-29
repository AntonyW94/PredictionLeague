using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ThePredictions.SchemaCheck;

/// <summary>
/// Works out what to declare each SQL parameter as for sp_describe_first_result_set. Types are read off
/// the anonymous object at the call site wherever possible (nameof(...) is a string, a numeric literal is
/// an int) and fall back to a name-based guess otherwise.
/// </summary>
public static class ParameterInferrer
{
    private const string StringType = "nvarchar(450)";
    private const string IntType = "int";
    private const string DecimalType = "decimal(18,2)";
    private const string DateType = "datetime2";
    private const string BitType = "bit";

    private static readonly Regex ParameterPattern = new(@"(?<!@)@(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex DeclarePattern = new(@"DECLARE\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<InferredParameter> Infer(string sql, AnonymousObjectCreationExpressionSyntax? anonymousObject)
    {
        var fromCallSite = ReadCallSiteTypes(anonymousObject);

        var declared = DeclarePattern.Matches(sql)
            .Select(m => m.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parameters = new List<InferredParameter>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in ParameterPattern.Matches(sql).Cast<Match>())
        {
            var name = match.Groups["name"].Value;

            if (declared.Contains(name) || !seen.Add(name))
                continue;

            if (fromCallSite.TryGetValue(name, out var known))
                parameters.Add(new InferredParameter(name, known, WasGuessed: false));
            else
                parameters.Add(new InferredParameter(name, GuessFromName(name), WasGuessed: true));
        }

        return parameters;
    }

    /// <summary>The other plausible typing for a guessed parameter, used to confirm a mismatch is real.</summary>
    public static string Alternative(string sqlType) => sqlType == IntType ? StringType : IntType;

    public static string Declarations(IEnumerable<InferredParameter> parameters) =>
        string.Join(", ", parameters.Select(p => $"@{p.Name} {p.SqlType}"));

    private static Dictionary<string, string> ReadCallSiteTypes(AnonymousObjectCreationExpressionSyntax? anonymousObject)
    {
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (anonymousObject is null)
            return types;

        foreach (var member in anonymousObject.Initializers)
        {
            var name = member.NameEquals?.Name.Identifier.ValueText
                ?? (member.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText
                ?? (member.Expression as IdentifierNameSyntax)?.Identifier.ValueText;

            if (name is null)
                continue;

            var inferred = InferFromExpression(member.Expression);
            if (inferred is not null)
                types[name] = inferred;
        }

        return types;
    }

    private static string? InferFromExpression(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return StringType;

            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.TrueLiteralExpression) || literal.IsKind(SyntaxKind.FalseLiteralExpression):
                return BitType;

            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression):
                {
                    var text = literal.Token.Text;
                    return text.Contains('.', StringComparison.Ordinal) || text.EndsWith('m') || text.EndsWith('M') ? DecimalType : IntType;
                }

            // nameof(RoundStatus.Completed) and value.ToString() are the two ways this codebase passes
            // enum names as parameters, and both arrive as strings.
            case InvocationExpressionSyntax invocation when invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }:
                return StringType;

            case InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ToString" }:
                return StringType;

            case CastExpressionSyntax cast:
                return SqlTypeMap.NormaliseClrType(cast.Type.ToString()) switch
                {
                    "int" or "short" or "byte" => IntType,
                    "long" => "bigint",
                    "decimal" => DecimalType,
                    "bool" => BitType,
                    "DateTime" => DateType,
                    _ => StringType
                };

            default:
                return null;
        }
    }

    private static string GuessFromName(string name)
    {
        if (name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Count", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Month", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Year", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Number", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Days", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Minutes", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Hours", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Limit", StringComparison.OrdinalIgnoreCase))
            return IntType;

        if (name.EndsWith("Utc", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Date", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Deadline", StringComparison.OrdinalIgnoreCase))
            return DateType;

        if (name.EndsWith("Price", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Amount", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Fee", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Cost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Fund", StringComparison.OrdinalIgnoreCase))
            return DecimalType;

        return StringType;
    }
}
