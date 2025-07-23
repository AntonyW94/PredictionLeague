using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record UpdateLeagueCommand(
    int Id,
    string Name,
    decimal Price,
    string? EntryCode,
    DateTime EntryDeadline) : IRequest;