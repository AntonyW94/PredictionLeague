using MediatR;

namespace ThePredictions.Application.Features.Admin.EmailSettings.Commands;

public record UpdateEmailSettingsCommand(bool EmailsEnabled) : IRequest;
