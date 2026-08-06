using ThePredictions.Contracts.Admin.PricingSettings;

namespace ThePredictions.Tests.Builders.Admin.PricingSettings;

public class UpdatePricingSettingsRequestBuilder
{
    private decimal _bufferRate = 0.15m;
    private decimal _minimumFloor = 25m;

    public UpdatePricingSettingsRequestBuilder WithBufferRate(decimal bufferRate)
    {
        _bufferRate = bufferRate;
        return this;
    }

    public UpdatePricingSettingsRequestBuilder WithMinimumFloor(decimal minimumFloor)
    {
        _minimumFloor = minimumFloor;
        return this;
    }

    public UpdatePricingSettingsRequest Build() => new()
    {
        BufferRate = _bufferRate,
        MinimumFloor = _minimumFloor
    };
}
