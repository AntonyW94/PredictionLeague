using MediatR;
using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

public record GetCompetitionByIdQuery(int Id) : IRequest<CompetitionDto?>;
