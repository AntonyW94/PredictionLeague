using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace PredictionLeague.Hosting.Shared.Extensions;

public static class ConfigurationSubstitutionExtensions
{
    private static readonly Regex PlaceholderRegex = new Regex(@"\$\{(?<key>[\w\:\-]+)\}", RegexOptions.Compiled);

    public static IConfiguration EnableSubstitutions(this IConfiguration configuration)
    {
        const int maxIterations = 10;
        var iterations = 0;
        bool changed;

        do
        {
            changed = false;

            foreach (var kvp in configuration.AsEnumerable())
            {
                if (string.IsNullOrWhiteSpace(kvp.Value))
                    continue;

                var match = PlaceholderRegex.Match(kvp.Value);
                if (match.Success)
                {
                    var keyToSubstitute = match.Groups["key"].Value;

                    var substitute = configuration[keyToSubstitute];
                    if (substitute != null && substitute != kvp.Value)
                    {
                        configuration[kvp.Key] = kvp.Value.Replace(match.Value, substitute);
                        changed = true;
                    }
                }
            }
            iterations++;
        } while (changed && iterations < maxIterations);

        return configuration;
    }
}