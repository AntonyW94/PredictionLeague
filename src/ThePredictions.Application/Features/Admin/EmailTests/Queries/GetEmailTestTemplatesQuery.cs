using MediatR;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Queries;

public record GetEmailTestTemplatesQuery : IRequest<IReadOnlyList<EmailTestTemplateDto>>;
