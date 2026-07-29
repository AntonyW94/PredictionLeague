using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ThePredictions.SchemaCheck;

/// <summary>
/// Resolves the SQL text behind a read call. The SQL is nearly always a `const string sql = @"..."`
/// declared in the same method, but it can also be a class-level constant, an interpolated string built
/// from one (`$"{GetRoundsWithMatchesSql} WHERE ..."`), or a concatenation. Resolution is scope-aware and
/// takes the declaration nearest above the call site, because these files declare the same identifier
/// once per method.
/// </summary>
public static class ConstantStringResolver
{
    public static string? Resolve(ExpressionSyntax expression, SyntaxNode callSite)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText;

            case InterpolatedStringExpressionSyntax interpolated:
                {
                    var builder = new System.Text.StringBuilder();

                    foreach (var content in interpolated.Contents)
                    {
                        switch (content)
                        {
                            case InterpolatedStringTextSyntax text:
                                builder.Append(text.TextToken.ValueText);
                                break;

                            case InterpolationSyntax interpolation:
                                {
                                    var resolved = Resolve(interpolation.Expression, callSite);
                                    if (resolved is null)
                                        return null;

                                    builder.Append(resolved);
                                    break;
                                }

                            default:
                                return null;
                        }
                    }

                    return builder.ToString();
                }

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                {
                    var left = Resolve(binary.Left, callSite);
                    var right = Resolve(binary.Right, callSite);

                    return left is null || right is null ? null : left + right;
                }

            case IdentifierNameSyntax identifier:
                {
                    var initialiser = FindInitialiser(identifier.Identifier.ValueText, callSite);
                    return initialiser is null ? null : Resolve(initialiser, callSite);
                }

            default:
                return null;
        }
    }

    /// <summary>
    /// Finds what an identifier was assigned, searching enclosing scopes outwards: locals declared above
    /// the call site first, then fields and constants on the containing type.
    /// </summary>
    public static ExpressionSyntax? FindInitialiser(string name, SyntaxNode callSite)
    {
        foreach (var ancestor in callSite.Ancestors())
        {
            var local = ancestor.DescendantNodes()
                .OfType<LocalDeclarationStatementSyntax>()
                .Where(d => d.SpanStart < callSite.SpanStart)
                .SelectMany(d => d.Declaration.Variables)
                .LastOrDefault(v => string.Equals(v.Identifier.ValueText, name, StringComparison.Ordinal) && v.Initializer is not null);

            if (local?.Initializer is not null)
                return local.Initializer.Value;

            if (ancestor is not TypeDeclarationSyntax type)
                continue;

            var field = type.Members
                .OfType<FieldDeclarationSyntax>()
                .SelectMany(f => f.Declaration.Variables)
                .FirstOrDefault(v => string.Equals(v.Identifier.ValueText, name, StringComparison.Ordinal) && v.Initializer is not null);

            if (field?.Initializer is not null)
                return field.Initializer.Value;
        }

        return null;
    }
}
