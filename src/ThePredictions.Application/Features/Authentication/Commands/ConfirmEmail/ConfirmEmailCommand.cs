using MediatR;

namespace ThePredictions.Application.Features.Authentication.Commands.ConfirmEmail;

public record ConfirmEmailCommand(string Token) : IRequest<Unit>;
