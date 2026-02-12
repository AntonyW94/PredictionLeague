using MediatR;
using Microsoft.Extensions.Logging;
using PredictionLeague.Application.Common.Interfaces;
using System.Transactions;

namespace PredictionLeague.Application.Common.Behaviours;

public class TransactionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>, ITransactionalRequest
{
    private readonly ILogger<TransactionBehaviour<TRequest, TResponse>> _logger;

    public TransactionBehaviour(ILogger<TransactionBehaviour<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            _logger.LogDebug("Beginning transaction for {RequestName}", requestName);

            var response = await next(cancellationToken);

            scope.Complete();

            _logger.LogDebug("Committed transaction for {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction for {RequestName} failed. Rolling back.", requestName);
            throw;
        }
    }
}