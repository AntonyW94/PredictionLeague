using MediatR;
using PredictionLeague.Contracts.Account;

namespace PredictionLeague.Application.Features.Account.Queries;

public record GetUserQuery(string UserId) : IRequest<UserDetails?>;