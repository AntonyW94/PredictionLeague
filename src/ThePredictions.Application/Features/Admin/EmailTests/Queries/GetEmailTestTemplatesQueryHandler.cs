using MediatR;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Queries;

public class GetEmailTestTemplatesQueryHandler(IEmailTemplateCatalog catalog)
    : IRequestHandler<GetEmailTestTemplatesQuery, IReadOnlyList<EmailTestTemplateDto>>
{
    public async Task<IReadOnlyList<EmailTestTemplateDto>> Handle(GetEmailTestTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await catalog.GetTemplatesAsync(cancellationToken);

        return templates
            .Select(t => new EmailTestTemplateDto(t.Id, t.Name, t.Subject, t.IsActive, t.ParamNames.ToList()))
            .ToList();
    }
}
