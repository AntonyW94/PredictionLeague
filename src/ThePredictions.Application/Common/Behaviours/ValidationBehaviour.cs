using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ThePredictions.Application.Common.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators, ILogger<ValidationBehaviour<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any()) 
            return await next(cancellationToken);
        
        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (!failures.Any())
            return await next(cancellationToken);
        
        // Information, matching LoggingBehaviour and the middleware: a form filled in wrong is the caller's
        // to fix and needs nobody to look at it. Warning is kept for what wants acting on. See ADR-0018.
        logger.LogInformation("Validation failed for {RequestName}. Errors: {@ValidationErrors}", typeof(TRequest).Name, failures);
        throw new ValidationException(failures);
    }
}