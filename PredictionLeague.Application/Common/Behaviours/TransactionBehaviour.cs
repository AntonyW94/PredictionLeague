using MediatR;
using PredictionLeague.Application.Common.Interfaces;
using System.Transactions;

namespace PredictionLeague.Application.Common.Behaviours;

public class TransactionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>, ITransactionalRequest
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        var response = await next(cancellationToken);

        scope.Complete();

        return response;
    }
}