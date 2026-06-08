namespace ThePredictions.Contracts.Admin.EmailTests;

public record EmailTestTemplateDto(long Id, string Name, string Subject, bool IsActive, List<string> ParamNames);
