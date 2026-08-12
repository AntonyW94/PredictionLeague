using Microsoft.CodeAnalysis.CSharp;
using ThePredictions.SchemaCheck;

var connectionString = ArgumentValue(args, "--connection") ?? Environment.GetEnvironmentVariable("PREDICTIONS_DEV_DB");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("No connection string. Set PREDICTIONS_DEV_DB or pass --connection \"<connection string>\".");
    return 2;
}

var root = ArgumentValue(args, "--root") ?? FindRepositoryRoot();
if (root is null)
{
    Console.Error.WriteLine("Could not find the repository root (looked for ThePredictions.sln). Pass --root <path>.");
    return 2;
}

var strict = args.Contains("--strict", StringComparer.Ordinal);
var sourceRoot = Path.Combine(root, "src");

var typeIndex = new TypeIndex();
var scanner = new CallSiteScanner();
var callSites = new List<ReadCallSite>();

foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
{
    if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        continue;

    var relativePath = Path.GetRelativePath(root, file);
    var tree = CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(file), path: file);

    typeIndex.Add(tree, relativePath);
    callSites.AddRange(scanner.Scan(tree, relativePath));
}

// --changed narrows the sweep to the reads a set of files could have affected, so a pre-commit hook can
// stay fast. A changed file matters if it holds the call site OR declares the type being materialised - a
// reshaped result record breaks the query in whichever file that query lives.
var changedFiles = ParseChangedFiles(ArgumentValue(args, "--changed"));

if (changedFiles is not null)
{
    var relevant = callSites
        .Where(c => changedFiles.Contains(NormalisePath(c.File))
                    || (typeIndex.Resolve(c.TypeArgument, c.File, out _) is { } shape && changedFiles.Contains(NormalisePath(shape.File))))
        .ToList();

    Console.WriteLine($"Filtered to {relevant.Count} of {callSites.Count} reads affected by {changedFiles.Count} changed file(s).");
    callSites = relevant;
}

var describer = new ResultSetDescriber(connectionString);

var connectionError = await describer.TestConnectionAsync(CancellationToken.None);
if (connectionError is not null)
{
    Console.Error.WriteLine($"Could not reach the database, so nothing was checked: {connectionError}");
    return 2;
}

var results = new List<CheckResult>();

var checkable = callSites.Count(c => c.SkipReason is null);
if (checkable == 0)
{
    Console.WriteLine("No checkable Dapper reads. Nothing to do.");
    return 0;
}

Console.WriteLine($"Checking {checkable} Dapper reads in {sourceRoot}...");

// Printed every run, so a stated exception stays arguable rather than becoming furniture.
foreach (var (column, reason) in NullabilityExceptions.All)
{
    Console.WriteLine();
    Console.WriteLine($"  Nullability exception: any column named '{column}'");
    Console.WriteLine($"      {reason}");
}
Console.WriteLine();

foreach (var callSite in callSites)
{
    if (callSite.SkipReason is not null)
    {
        results.Add(new CheckResult(callSite, CheckStatus.Skipped, callSite.SkipReason));
        continue;
    }

    var (columns, error) = await describer.DescribeAsync(callSite.Sql!, ParameterInferrer.Declarations(callSite.Parameters), CancellationToken.None);
    if (columns is null)
    {
        results.Add(new CheckResult(callSite, CheckStatus.Skipped, $"could not be described: {error}"));
        continue;
    }

    var result = MappingChecker.Check(callSite, typeIndex, columns);

    // A guessed parameter type can change the described type of a CASE expression that returns it, so a
    // mismatch is only reported once it survives the other plausible typing.
    if (result.Status is CheckStatus.Mismatch or CheckStatus.Broken && callSite.Parameters.Any(p => p.WasGuessed))
    {
        var alternatives = callSite.Parameters
            .Select(p => p.WasGuessed ? p with { SqlType = ParameterInferrer.Alternative(p.SqlType) } : p)
            .ToList();

        var (alternativeColumns, _) = await describer.DescribeAsync(callSite.Sql!, ParameterInferrer.Declarations(alternatives), CancellationToken.None);

        if (alternativeColumns is not null && MappingChecker.Check(callSite, typeIndex, alternativeColumns).Status == CheckStatus.Ok)
            result = new CheckResult(callSite, CheckStatus.Review, $"only mismatches under one guessed parameter typing: {result.Detail}");
    }

    results.Add(result);
}

var order = new[] { CheckStatus.Broken, CheckStatus.Mismatch, CheckStatus.SilentDrop, CheckStatus.Review, CheckStatus.Skipped, CheckStatus.Ok };

Console.WriteLine("SUMMARY");
foreach (var status in order)
{
    var count = results.Count(r => r.Status == status);
    if (count > 0)
        Console.WriteLine($"  {status,-11} {count}");
}

foreach (var status in order.Where(s => s != CheckStatus.Ok))
{
    var matching = results.Where(r => r.Status == status).ToList();
    if (matching.Count == 0)
        continue;

    // Scalars cannot break, so they are counted rather than listed; everything else is named.
    var listed = status == CheckStatus.Skipped
        ? matching.Where(r => r.Detail != "scalar").ToList()
        : matching;

    if (status == CheckStatus.Skipped && matching.Count != listed.Count)
        Console.WriteLine($"{Environment.NewLine}{status.ToString().ToUpperInvariant()} ({matching.Count - listed.Count} scalar reads not listed)");
    else
        Console.WriteLine($"{Environment.NewLine}{status.ToString().ToUpperInvariant()}");

    foreach (var result in listed.OrderBy(r => r.CallSite.File, StringComparer.Ordinal).ThenBy(r => r.CallSite.Line))
    {
        Console.WriteLine($"  {result.CallSite.TypeArgument}  [{result.CallSite.Location}]");
        Console.WriteLine($"      {result.Detail}");
    }
}

var failures = results.Count(r => r.IsFailure) + (strict ? results.Count(r => r.Status == CheckStatus.SilentDrop) : 0);

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "No materialisation failures found."
    : $"{failures} problem(s) that need fixing.");

return failures == 0 ? 0 : 1;

static HashSet<string>? ParseChangedFiles(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return null;

    return value
        .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalisePath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

// git reports forward slashes relative to the repository root; Path.GetRelativePath uses the platform
// separator, so both sides are normalised before comparing.
static string NormalisePath(string path) => path.Replace('\\', '/').Trim();

static string? ArgumentValue(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);

    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static string? FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "ThePredictions.sln")))
            return directory.FullName;

        directory = directory.Parent;
    }

    return null;
}
