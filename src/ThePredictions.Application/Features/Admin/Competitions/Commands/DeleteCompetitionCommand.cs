using MediatR;

namespace ThePredictions.Application.Features.Admin.Competitions.Commands;

public record DeleteCompetitionCommand(int Id) : IRequest;
