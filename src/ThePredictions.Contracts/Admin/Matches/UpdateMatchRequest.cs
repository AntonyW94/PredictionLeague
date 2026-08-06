using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Admin.Matches;

[ExcludeFromCodeCoverage]
public class UpdateMatchRequest : BaseMatchRequest
{
    public int Id { get; init; }
}
