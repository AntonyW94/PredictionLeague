using MediatR;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonCommand : UpdateSeasonRequest, IRequest
{
    public int Id { get; init; }
}