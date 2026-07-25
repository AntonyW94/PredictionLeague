using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Queries;

public class GetEmailTestDefaultsQueryHandler(
    IEmailTemplateCatalog catalog,
    IApplicationReadDbConnection readDb,
    IEmailTestDefaultsResolver resolver,
    IOptions<SiteSettings> siteSettings)
    : IRequestHandler<GetEmailTestDefaultsQuery, EmailTestDefaultsDto>
{
    public async Task<EmailTestDefaultsDto> Handle(GetEmailTestDefaultsQuery request, CancellationToken cancellationToken)
    {
        var templates = await catalog.GetTemplatesAsync(cancellationToken);
        var template = templates.FirstOrDefault(t => t.Id == request.TemplateId);
        if (template is null)
            return new EmailTestDefaultsDto(new Dictionary<string, string>());

        // SELECT column order must match the EmailTestUserData constructor (FirstName, LastName, Email).
        var user = await readDb.QuerySingleOrDefaultAsync<EmailTestUserData>(
            @"
                SELECT
                    u.[FirstName],
                    u.[LastName],
                    u.[Email]
                FROM
                    [AspNetUsers] u
                WHERE
                    u.[Id] = @UserId",
            cancellationToken,
            new { UserId = request.DataUserId })
            ?? new EmailTestUserData(string.Empty, string.Empty, string.Empty);

        var baseUrl = siteSettings.Value.ResolvedBaseUrl;

        var defaults = resolver.Resolve(template.ParamNames, user, baseUrl);
        return new EmailTestDefaultsDto(new Dictionary<string, string>(defaults));
    }
}
