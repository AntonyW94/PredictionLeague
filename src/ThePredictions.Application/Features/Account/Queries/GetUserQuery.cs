using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Account;

namespace ThePredictions.Application.Features.Account.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetUserQuery(string UserId) : IRequest<UserDetails>;
