using MediatR;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public record GetRoundByIdQuery(int Id) : IRequest<RoundDetailsDto?>;