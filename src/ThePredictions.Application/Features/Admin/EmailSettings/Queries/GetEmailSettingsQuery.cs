using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.EmailSettings;

namespace ThePredictions.Application.Features.Admin.EmailSettings.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetEmailSettingsQuery : IRequest<EmailSettingsDto>;
