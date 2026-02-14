using PredictionLeague.Domain.Common;

namespace ThePredictions.Tests.Shared.Helpers;

public class TestDateTimeProvider : IDateTimeProvider
{
    public TestDateTimeProvider(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }

    public void AdvanceBy(TimeSpan duration)
    {
        UtcNow = UtcNow.Add(duration);
    }
}
