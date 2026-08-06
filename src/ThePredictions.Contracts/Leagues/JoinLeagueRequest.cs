using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class JoinLeagueRequest
{
    public required string EntryCode { get; set; }
}
