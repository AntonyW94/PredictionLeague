using System.Diagnostics.CodeAnalysis;
using ThePredictions.Contracts.Prizes;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class CreateLeagueRequest : IHasBankDetails
{
    public int SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime EntryDeadlineUtc { get; set; }
    public int PointsForExactScore { get; set; }
    public int PointsForCorrectResult { get; set; }

    // Optional money the admin puts up on top of the entry fees (added to the pot: Price x N + this).
    public decimal? PrizeFundOverride { get; set; }

    // Optional peer-to-peer entry-fee bank details (plaintext in transit; encrypted server-side at rest).
    public string? BankAccountName { get; set; }
    public string? BankSortCode { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? PaymentReferenceTemplate { get; set; }

    // Optional up-front prize scheme (write-once). When supplied it locks at creation.
    public PrizeSchemeRequest? PrizeScheme { get; set; }
}
