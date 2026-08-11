using ThePredictions.Application.Services;

namespace ThePredictions.Application.Features.Admin.EmailTests.Queries;

/// <summary>
/// Reads the player whose details stand in for the merge fields when an administrator previews an email, or nothing if
/// there is no such account.
/// </summary>
/// <remarks>
/// What to show for an account that is not there - empty fields rather than a failure, so the preview still renders - is
/// the handler's rule.
/// </remarks>
public interface IEmailTestUserQuery
{
    Task<EmailTestUserData?> ExecuteAsync(string userId, CancellationToken cancellationToken);
}
