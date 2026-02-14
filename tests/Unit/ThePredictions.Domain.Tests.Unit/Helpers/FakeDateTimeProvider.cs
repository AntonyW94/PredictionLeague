using PredictionLeague.Domain.Common;

namespace ThePredictions.Domain.Tests.Unit.Helpers;

public class FakeDateTimeProvider : IDateTimeProvider
{
    public FakeDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}
