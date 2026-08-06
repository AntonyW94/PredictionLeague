using System.Diagnostics.CodeAnalysis;
using ThePredictions.Domain.Common;

namespace ThePredictions.Infrastructure;

[ExcludeFromCodeCoverage(Justification = "Returns DateTime.UtcNow: nothing to assert that is not a tautology.")]
public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
