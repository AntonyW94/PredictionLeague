using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class UpdateLeagueCommand : UpdateLeagueRequest, IRequest
{
    public int Id { get; }

    public UpdateLeagueCommand(int id, UpdateLeagueRequest request)
    {
        Id = id;
        Name = request.Name;
        EntryCode = request.EntryCode;
    }
}