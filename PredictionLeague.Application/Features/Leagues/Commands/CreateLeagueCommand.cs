using MediatR;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Models;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class CreateLeagueCommand : CreateLeagueRequest, IRequest<League>
{
    public string CreatingUserId { get; }

    public CreateLeagueCommand(CreateLeagueRequest request, string creatingUserId)
    {
        SeasonId = request.SeasonId;
        Name = request.Name;
        EntryCode = request.EntryCode;
        CreatingUserId = creatingUserId;
    }
}