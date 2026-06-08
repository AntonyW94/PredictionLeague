namespace ThePredictions.Application.Services;

/// <summary>
/// Produces realistic default values for a template's merge-tag parameters, so the admin
/// email-test form is pre-filled rather than blank. Generic name-matching (e.g. FIRST_NAME)
/// is seeded from the selected user; link-style params are built from the site base URL.
/// </summary>
public interface IEmailTestDefaultsResolver
{
    IReadOnlyDictionary<string, string> Resolve(IReadOnlyList<string> paramNames, EmailTestUserData user, string baseUrl);
}
