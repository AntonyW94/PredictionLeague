using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Application.Features.Admin.ServiceFees.Queries;

public class GetServiceFeesQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetServiceFeesQuery, IEnumerable<ServiceFeeDto>>
{
    public async Task<IEnumerable<ServiceFeeDto>> Handle(GetServiceFeesQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sf.[Provider],
                sf.[PercentFee],
                sf.[FixedFee]
            FROM
                [ServiceFees] sf
            ORDER BY
                sf.[Provider];";

        var fees = await dbConnection.QueryAsync<ServiceFeeQueryResult>(sql, cancellationToken);

        return fees.Select(f => new ServiceFeeDto(
            f.Provider,
            f.PercentFee,
            f.FixedFee));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record ServiceFeeQueryResult(
        string Provider,
        decimal PercentFee,
        decimal FixedFee);
}
