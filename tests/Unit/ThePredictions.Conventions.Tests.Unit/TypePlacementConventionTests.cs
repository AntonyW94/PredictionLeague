using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ThePredictions.Conventions.Tests.Unit;

/// <summary>
/// Two placement rules from the root CLAUDE.md that nothing enforced. Both failed in practice: the
/// audit that added this project found two <c>Dto</c>-suffixed records declared public at the bottom of
/// command handler files, which broke both rules at once and let the fragile positional
/// SELECT-to-record coupling escape the file the convention exists to confine it to.
/// </summary>
public partial class TypePlacementConventionTests
{
    // Matches a top-level type declaration - column 0, so nested types (always indented under a
    // file-scoped namespace) do not count.
    [GeneratedRegex(@"^public\s+(?:[a-z]+\s+)*(?:class|record|struct|interface|enum|delegate)\s",
        RegexOptions.Multiline)]
    private static partial Regex TopLevelPublicTypeDeclaration();

    // "NEVER put multiple public types in one file" - root CLAUDE.md, Things to NEVER Do #6.
    [Fact]
    public void EverySourceFile_ShouldDeclareAtMostOnePublicType()
    {
        var offenders = ProductionAssemblies.SourceFiles(".cs")
            .Select(f => (f.RelativePath, Count: TopLevelPublicTypeDeclaration().Matches(f.Text).Count))
            .Where(f => f.Count > 1)
            .Select(f => $"{f.RelativePath} declares {f.Count} public types")
            .OrderBy(f => f)
            .ToList();

        offenders.Should().BeEmpty(
            "one public type per file keeps a type findable by its file name - split the extra types out.");
    }

    // The Dto suffix names the outward contract shared with the browser. A Dto elsewhere is either a
    // Dapper row type in disguise (name it XxxRow or XxxQueryResult and make it internal) or a contract
    // that belongs in ThePredictions.Contracts.
    [Fact]
    public void OnlyContracts_ShouldDeclareDtoSuffixedTypes()
    {
        var misplaced = ProductionAssemblies.All
            .Where(a => a != ProductionAssemblies.Contracts)
            .SelectMany(a => a.GetTypes().Select(t => (Assembly: a, Type: t)))
            .Where(x => !IsCompilerGenerated(x.Type))
            .Where(x => x.Type.Name.EndsWith("Dto", StringComparison.Ordinal)
                        || x.Type.Name.EndsWith("Dtos", StringComparison.Ordinal))
            .Select(x => $"{x.Assembly.GetName().Name}: {x.Type.FullName}")
            .OrderBy(n => n)
            .ToList();

        misplaced.Should().BeEmpty(
            "the Dto suffix is reserved for ThePredictions.Contracts. A handler's own row type should be "
            + "internal and named XxxRow or XxxQueryResult, per the Dapper result-mapping rule in CLAUDE.md.");
    }

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttribute<CompilerGeneratedAttribute>(inherit: false) != null
        || member.Name.Contains('<');
}
