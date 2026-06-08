namespace ThePredictions.Application.Services;

/// <summary>
/// A Brevo transactional template as discovered from the Brevo API, including the
/// merge-tag parameter names extracted from its HTML (every distinct <c>{{ params.X }}</c>).
/// </summary>
public record EmailTemplateInfo(long Id, string Name, string Subject, bool IsActive, IReadOnlyList<string> ParamNames);
