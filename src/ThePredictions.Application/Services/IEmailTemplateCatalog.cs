namespace ThePredictions.Application.Services;

/// <summary>
/// Discovers the available Brevo transactional templates and the merge-tag parameters
/// each one expects. Backs the admin email-test tool so newly-created templates appear
/// with no code change.
/// </summary>
public interface IEmailTemplateCatalog
{
    Task<IReadOnlyList<EmailTemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken);
}
