using MediatR;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class UpdateSeasonCommand : UpdateSeasonRequest, IRequest
{
    public int Id { get; }
    
    public UpdateSeasonCommand(int id, UpdateSeasonRequest request)
    {
        Id = id;
        Name = request.Name;
        StartDate = request.StartDate;
        EndDate = request.EndDate;
        IsActive = request.IsActive;
    }
}