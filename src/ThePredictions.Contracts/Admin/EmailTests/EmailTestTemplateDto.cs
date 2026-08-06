using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.EmailTests;

[ExcludeFromCodeCoverage]
public record EmailTestTemplateDto(long Id, string Name, string Subject, bool IsActive, List<string> ParamNames);
