using MediatR;
using Microsoft.Extensions.Options;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Queries;

public class GetEmailTestDefaultsQueryHandler(
    IEmailTemplateCatalog catalog,
    IEmailTestUserQuery emailTestUserQuery,
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

        // An account that is not there still has to render a preview, so the merge fields come back empty rather than
        // the request failing.
        var user = await emailTestUserQuery.ExecuteAsync(request.DataUserId, cancellationToken)
                   ?? new EmailTestUserData(string.Empty, string.Empty, string.Empty);

        var baseUrl = siteSettings.Value.ResolvedBaseUrl;

        var defaults = resolver.Resolve(template.ParamNames, user, baseUrl);
        return new EmailTestDefaultsDto(new Dictionary<string, string>(defaults));
    }
}
