using MediatR;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public class GetSeasonByIdQuery : IRequest<SeasonDto?>
{
    public int Id { get; init; }
}
