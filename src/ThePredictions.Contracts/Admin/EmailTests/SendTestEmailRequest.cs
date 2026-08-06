using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailTests;

[ExcludeFromCodeCoverage]
public record SendTestEmailRequest(long TemplateId, Dictionary<string, string> Parameters);
