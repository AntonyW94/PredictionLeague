using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class CreateSeasonCommand : CreateSeasonRequest, IRequest, ITransactionalRequest
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