using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Leagues;

[ExcludeFromCodeCoverage]
public class UpdateLeagueRequest : IHasBankDetails
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime EntryDeadlineUtc { get; set; }
    public int PointsForExactScore { get; set; }
    public int PointsForCorrectResult { get; set; }

    // When true, new joiners must be approved by the admin. When false, they are approved automatically.
    public bool RequiresMemberApproval { get; set; } = true;

    // When true, a private league appears in the Available Leagues list (the entry code is still required
    // to join). Ignored for public leagues, which are always discoverable.
    public bool IsListed { get; set; }

    // Optional money the admin puts up on top of the entry fees (added to the pot: Price x N + this).
    public decimal? PrizeFundOverride { get; set; }

    // Optional peer-to-peer entry-fee bank details (plaintext in transit; encrypted server-side at rest).
    public string? BankAccountName { get; set; }
    public string? BankSortCode { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? PaymentReferenceTemplate { get; set; }
}
