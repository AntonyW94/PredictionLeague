using MediatR;
using PredictionLeague.Contracts.Admin.Users;

namespace PredictionLeague.Application.Features.Admin.Users.Queries;

public record GetAllUsersQuery : IRequest<IEnumerable<UserDto>>;