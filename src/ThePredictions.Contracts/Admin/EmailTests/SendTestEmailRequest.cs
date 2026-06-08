namespace ThePredictions.Contracts.Admin.EmailTests;

public record SendTestEmailRequest(long TemplateId, Dictionary<string, string> Parameters);
