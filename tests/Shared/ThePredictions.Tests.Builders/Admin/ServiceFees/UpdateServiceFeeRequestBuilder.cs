using ThePredictions.Contracts.Admin.ServiceFees;

namespace ThePredictions.Tests.Builders.Admin.ServiceFees;

public class UpdateServiceFeeRequestBuilder
{
    private decimal _percentFee = 0.015m;
    private decimal _fixedFee = 0.20m;

    public UpdateServiceFeeRequestBuilder WithPercentFee(decimal percentFee)
    {
        _percentFee = percentFee;
        return this;
    }

    public UpdateServiceFeeRequestBuilder WithFixedFee(decimal fixedFee)
    {
        _fixedFee = fixedFee;
        return this;
    }

    public UpdateServiceFeeRequest Build() => new()
    {
        PercentFee = _percentFee,
        FixedFee = _fixedFee
    };
}
