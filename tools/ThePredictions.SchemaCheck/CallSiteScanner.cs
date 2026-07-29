using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ThePredictions.SchemaCheck;

/// <summary>
/// Finds every Dapper read in a syntax tree and pulls out the SQL and parameters behind it.
/// </summary>
public sealed class CallSiteScanner
{
    private static readonly HashSet<string> ReadMethods = new(StringComparer.Ordinal)
    {
        "QueryAsync",
        "QuerySingleOrDefaultAsync",
        "QueryFirstOrDefaultAsync",
        "QuerySingleAsync",
        "QueryFirstAsync",
        "Query",
        "QuerySingleOrDefault",
        "QueryFirstOrDefault",
        "QuerySingle",
        "QueryFirst"
    };

    private static readonly HashSet<string> BatchReadMethods = new(StringComparer.Ordinal)
    {
        "ReadAsync",
        "ReadSingleOrDefaultAsync",
        "ReadFirstOrDefaultAsync",
        "ReadSingleAsync",
        "ReadFirstAsync"
    };

    public List<ReadCallSite> Scan(SyntaxTree tree, string relativePath)
    {
        var callSites = new List<ReadCallSite>();

        foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } )
                continue;

            var method = generic.Identifier.ValueText;
            var isBatchRead = BatchReadMethods.Contains(method);

            if (!ReadMethods.Contains(method) && !isBatchRead)
                continue;

            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var typeArguments = generic.TypeArgumentList.Arguments;

            if (typeArguments.Count == 0)
                continue;

            if (typeArguments.Count > 1)
            {
                callSites.Add(Skipped(relativePath, line, method, string.Join(", ", typeArguments), "multi-mapping overload: each type takes a slice of the columns decided by splitOn"));
                continue;
            }

            var typeArgument = typeArguments[0];
            var typeText = typeArgument.ToString();

            if (typeArgument is TupleTypeSyntax tuple)
            {
                var elements = tuple.Elements.Select(e => e.Type.ToString()).ToList();
                var (tupleSql, tupleParameters) = ResolveCommand(invocation);

                callSites.Add(tupleSql is null
                    ? Skipped(relativePath, line, method, typeText, "SQL is not a resolvable compile-time constant")
                    : new ReadCallSite(relativePath, line, method, typeText, elements, tupleSql, ParameterInferrer.Infer(tupleSql, tupleParameters), null));
                continue;
            }

            if (SqlTypeMap.IsScalar(typeText))
            {
                callSites.Add(Skipped(relativePath, line, method, typeText, "scalar"));
                continue;
            }

            if (isBatchRead)
            {
                callSites.Add(Skipped(relativePath, line, method, typeText, "QueryMultiple batch: sp_describe_first_result_set only describes a batch's first statement"));
                continue;
            }

            var (sql, parameters) = ResolveCommand(invocation);

            callSites.Add(sql is null
                ? Skipped(relativePath, line, method, typeText, "SQL is not a resolvable compile-time constant")
                : new ReadCallSite(relativePath, line, method, typeText, null, sql, ParameterInferrer.Infer(sql, parameters), null));
        }

        return callSites;
    }

    private static ReadCallSite Skipped(string file, int line, string method, string typeArgument, string reason) =>
        new(file, line, method, typeArgument, null, null, [], reason);

    /// <summary>
    /// Reads the SQL and the parameter object out of a call, unwrapping a CommandDefinition - inline or
    /// held in a local - which is how the repositories pass their transaction and cancellation token.
    /// </summary>
    private static (string? Sql, AnonymousObjectCreationExpressionSyntax? Parameters) ResolveCommand(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
            return (null, null);

        var first = arguments[0].Expression;
        var commandDefinition = AsCommandDefinition(first, invocation);

        if (commandDefinition is not null)
        {
            var commandArguments = commandDefinition.ArgumentList?.Arguments ?? default;

            var sqlArgument = NamedOrPositional(commandArguments, "commandText", 0);
            var parameterArgument = NamedOrPositional(commandArguments, "parameters", 1);

            return (
                sqlArgument is null ? null : ConstantStringResolver.Resolve(sqlArgument, invocation),
                parameterArgument as AnonymousObjectCreationExpressionSyntax);
        }

        var anonymous = arguments
            .Skip(1)
            .Select(a => a.Expression)
            .OfType<AnonymousObjectCreationExpressionSyntax>()
            .FirstOrDefault();

        return (ConstantStringResolver.Resolve(first, invocation), anonymous);
    }

    private static ObjectCreationExpressionSyntax? AsCommandDefinition(ExpressionSyntax expression, SyntaxNode callSite)
    {
        if (expression is ObjectCreationExpressionSyntax direct && IsCommandDefinition(direct))
            return direct;

        if (expression is not IdentifierNameSyntax identifier)
            return null;

        var initialiser = ConstantStringResolver.FindInitialiser(identifier.Identifier.ValueText, callSite);

        return initialiser is ObjectCreationExpressionSyntax indirect && IsCommandDefinition(indirect) ? indirect : null;
    }

    private static bool IsCommandDefinition(ObjectCreationExpressionSyntax creation) =>
        creation.Type.ToString().EndsWith("CommandDefinition", StringComparison.Ordinal);

    private static ExpressionSyntax? NamedOrPositional(SeparatedSyntaxList<ArgumentSyntax> arguments, string name, int position)
    {
        var named = arguments.FirstOrDefault(a => string.Equals(a.NameColon?.Name.Identifier.ValueText, name, StringComparison.Ordinal));
        if (named is not null)
            return named.Expression;

        var positional = arguments.Where(a => a.NameColon is null).ToList();

        return position < positional.Count ? positional[position].Expression : null;
    }
}
