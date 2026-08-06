using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.EmailTests;

namespace ThePredictions.Application.Features.Admin.EmailTests.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record SendTestEmailCommand(long TemplateId, Dictionary<string, string> Parameters, string CallerUserId) : IRequest<SendTestEmailResultDto>;
