using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Configuration;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class BrevoSettings
{
    public string? ApiKey { get; init; }
    public string? SendFromName { get; init; }
    public string? SendFromEmail { get; init; }
    public TemplateSettings? Templates { get; init; }
}