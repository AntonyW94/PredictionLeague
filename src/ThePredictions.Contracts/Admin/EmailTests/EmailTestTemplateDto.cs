using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailTests;

[ExcludeFromCodeCoverage(Justification = "Data-only contract: properties only, no logic to test.")]
public record EmailTestTemplateDto(long Id, string Name, string Subject, bool IsActive, List<string> ParamNames);
