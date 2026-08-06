using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[ExcludeFromCodeCoverage(Justification = "Options type bound from configuration: properties only, no logic to test.")]
public class BrevoSettings
{
    public string? ApiKey { get; init; }
    public string? SendFromName { get; init; }
    public string? SendFromEmail { get; init; }
    public TemplateSettings? Templates { get; init; }
}