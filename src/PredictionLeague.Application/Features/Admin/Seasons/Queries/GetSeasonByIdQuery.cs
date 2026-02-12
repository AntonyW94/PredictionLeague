using MediatR;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public record GetSeasonByIdQuery(int Id) : IRequest<SeasonDto?>;