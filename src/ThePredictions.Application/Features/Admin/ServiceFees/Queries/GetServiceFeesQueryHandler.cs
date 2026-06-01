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

        return await dbConnection.QueryAsync<ServiceFeeDto>(sql, cancellationToken);
    }
}
