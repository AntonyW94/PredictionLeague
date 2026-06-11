using MediatR;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

public record SendLeagueWelcomeEmailsCommand : IRequest<SendLeagueWelcomeEmailsResult>;
