using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class JoinLeagueCommand : IRequest
{
    public int? LeagueId { get; }
    public string? EntryCode { get; }
    public string JoiningUserId { get; }

    public JoinLeagueCommand(int leagueId, string joiningUserId)
    {
        LeagueId = leagueId;
        JoiningUserId = joiningUserId;
    }

    public JoinLeagueCommand(string entryCode, string joiningUserId)
    {
        EntryCode = entryCode;
        JoiningUserId = joiningUserId;
    }
}