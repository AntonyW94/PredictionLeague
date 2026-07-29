using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ThePredictions.SchemaCheck;

/// <summary>
/// Every type declared in the scanned source, indexed by name. Resolution is same-file-first because
/// several handlers declare a private record of the same name (RoundQueryResult, UserQueryResult), and
/// base types resolve by name because the codebase keeps one public type per file.
/// </summary>
public sealed class TypeIndex
{
    private readonly Dictionary<string, List<TypeShape>> _byName = new(StringComparer.Ordinal);

    public void Add(SyntaxTree tree, string relativePath)
    {
        var root = tree.GetRoot();

        foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var shape = Describe(declaration, relativePath);

            if (!_byName.TryGetValue(shape.Name, out var list))
            {
                list = [];
                _byName[shape.Name] = list;
            }

            list.Add(shape);
        }
    }

    /// <summary>Resolves a type name, preferring a declaration in the same file as the call site.</summary>
    public TypeShape? Resolve(string name, string callSiteFile, out string? ambiguityReason)
    {
        ambiguityReason = null;

        if (!_byName.TryGetValue(name, out var candidates))
        {
            ambiguityReason = $"type '{name}' is not declared in the scanned source (framework or external type)";
            return null;
        }

        var sameFile = candidates.Where(c => string.Equals(c.File, callSiteFile, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameFile.Count == 1)
            return sameFile[0];

        if (candidates.Count == 1)
            return candidates[0];

        ambiguityReason = $"type '{name}' is declared in {candidates.Count} files and none in the call site's file";
        return null;
    }

    /// <summary>
    /// Members Dapper can assign by name, including everything inherited. If a base type is not in the
    /// scanned source - ApplicationUser derives from Identity's IdentityUser, which owns Id, Email,
    /// PasswordHash and the rest - its members are invisible here, so the caller is told rather than being
    /// handed a member list that is missing half the type.
    /// </summary>
    public (List<MemberShape> Settable, List<string> ReadOnly, string? UnresolvedBaseType) ResolveMembers(TypeShape shape)
    {
        var settable = new List<MemberShape>();
        var readOnly = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        var current = shape;
        while (current is not null && visited.Add(current.Name))
        {
            settable.AddRange(current.SettableMembers);
            readOnly.AddRange(current.ReadOnlyMembers);

            if (current.BaseTypeName is null)
                return (settable, readOnly, null);

            if (!_byName.TryGetValue(current.BaseTypeName, out var bases) || bases.Count != 1)
                return (settable, readOnly, current.BaseTypeName);

            current = bases[0];
        }

        return (settable, readOnly, null);
    }

    private static TypeShape Describe(TypeDeclarationSyntax declaration, string relativePath)
    {
        var constructors = new List<ConstructorShape>();

        // A record's positional parameter list, or a class's primary constructor, is a public constructor.
        var primaryParameters = declaration switch
        {
            RecordDeclarationSyntax record => record.ParameterList,
            ClassDeclarationSyntax @class => @class.ParameterList,
            _ => null
        };

        if (primaryParameters is not null)
            constructors.Add(new ConstructorShape(ConstructorShape.PublicRank, ToParameters(primaryParameters)));

        foreach (var constructor in declaration.Members.OfType<ConstructorDeclarationSyntax>())
        {
            if (constructor.Modifiers.Any(SyntaxKind.StaticKeyword))
                continue;

            constructors.Add(new ConstructorShape(RankOf(constructor.Modifiers), ToParameters(constructor.ParameterList)));
        }

        // A type that declares no instance constructor and has no primary constructor gets the implicit
        // public parameterless one - that is what puts records with `{ get; init; }` properties, and the
        // Contracts DTOs, on Dapper's name-mapping path rather than its positional one.
        if (constructors.Count == 0)
            constructors.Add(new ConstructorShape(ConstructorShape.PublicRank, []));

        // A record with a positional parameter list also gets an implicit copy constructor, which Dapper
        // would never match, so it is deliberately not modelled here.

        var settable = new List<MemberShape>();
        var readOnly = new List<string>();

        foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (property.Modifiers.Any(SyntaxKind.StaticKeyword) || !property.Modifiers.Any(SyntaxKind.PublicKeyword))
                continue;

            var name = property.Identifier.ValueText;
            var type = property.Type.ToString();
            var accessors = property.AccessorList?.Accessors;

            var hasSetter = accessors?.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration)) ?? false;

            if (hasSetter)
                settable.Add(new MemberShape(name, type));
            else
                readOnly.Add(name);
        }

        // Positional record parameters become public init-only properties, so they are settable by name too.
        if (primaryParameters is not null)
        {
            foreach (var parameter in ToParameters(primaryParameters))
            {
                if (!settable.Any(m => string.Equals(m.Name, parameter.Name, StringComparison.Ordinal)))
                    settable.Add(new MemberShape(parameter.Name, parameter.Type));
            }
        }

        var baseTypeName = declaration.BaseList?.Types
            .Select(t => t.Type)
            .OfType<IdentifierNameSyntax>()
            .Select(t => t.Identifier.ValueText)
            .FirstOrDefault(n => !n.StartsWith('I') || n.Length < 2 || !char.IsUpper(n[1]));

        return new TypeShape(declaration.Identifier.ValueText, relativePath, constructors, settable, readOnly, baseTypeName);
    }

    private static int RankOf(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))
            return ConstructorShape.PublicRank;

        if (modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Count == 0)
            return ConstructorShape.PrivateRank;

        return ConstructorShape.InternalRank;
    }

    private static List<ParameterShape> ToParameters(ParameterListSyntax parameterList) =>
        parameterList.Parameters
            .Select(p => new ParameterShape(p.Type?.ToString() ?? "object", p.Identifier.ValueText))
            .ToList();
}
