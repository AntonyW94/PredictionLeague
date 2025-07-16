using MediatR;
using PredictionLeague.Contracts.Admin.Teams;

namespace PredictionLeague.Application.Features.Admin.Teams.Queries;

public class FetchAllTeamsQuery : IRequest<IEnumerable<TeamDto>>;