using MediatR;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Queries;

public record GetEmailTestDefaultsQuery(long TemplateId, string DataUserId) : IRequest<EmailTestDefaultsDto>;
