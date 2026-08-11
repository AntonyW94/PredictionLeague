namespace ThePredictions.Application.Features.Admin.ServiceFees.Queries;

/// <summary>Reads every provider's fees, in no order.</summary>
public interface IServiceFeesQuery
{
    Task<IReadOnlyList<ServiceFeeRow>> ExecuteAsync(CancellationToken cancellationToken);
}
