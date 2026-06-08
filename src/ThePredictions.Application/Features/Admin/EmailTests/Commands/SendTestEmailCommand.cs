using MediatR;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Commands;

public record SendTestEmailCommand(long TemplateId, Dictionary<string, string> Parameters, string CallerUserId) : IRequest<SendTestEmailResultDto>;
