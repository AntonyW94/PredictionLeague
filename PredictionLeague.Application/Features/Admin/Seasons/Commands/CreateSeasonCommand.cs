using MediatR;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommand : CreateSeasonRequest, IRequest
{
    public string CreatorId { get; }

    public CreateSeasonCommand(CreateSeasonRequest request, string creatorId)
    {
        Name = request.Name;
        StartDate = request.StartDate;
        EndDate = request.EndDate;
        CreatorId = creatorId;
    }
}