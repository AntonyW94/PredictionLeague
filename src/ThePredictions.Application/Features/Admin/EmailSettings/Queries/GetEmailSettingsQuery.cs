using MediatR;
using ThePredictions.Contracts.Admin.EmailSettings;

namespace ThePredictions.Application.Features.Admin.EmailSettings.Queries;

public record GetEmailSettingsQuery : IRequest<EmailSettingsDto>;
