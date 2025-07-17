using MediatR;
using PredictionLeague.Contracts.Admin.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class UpdateLeagueCommand : UpdateLeagueRequest, IRequest
{
    public int Id { get; set; }

    public UpdateLeagueCommand(int id, UpdateLeagueRequest request)
    {
        Id = id;
        Name = request.Name;
        EntryCode = request.EntryCode;
    }
}