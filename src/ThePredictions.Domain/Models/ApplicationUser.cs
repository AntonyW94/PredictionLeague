using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Identity;

namespace ThePredictions.Domain.Models;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PreferredTheme { get; set; } = "light";
    public DateTime? TermsAcceptedAtUtc { get; set; }
    public DateTime? MarketingOptInAtUtc { get; set; }

    /// <summary>When this account was registered.</summary>
    /// <remarks>
    /// Nullable only because of the accounts that predate the column. Migration 0011 backfilled those from the earliest
    /// date the database can prove the account existed, and left it null for the few with no evidence at all - which is
    /// why a reader has to handle "unknown" rather than assume a date. Every account created since is stamped by
    /// <see cref="RecordRegistration"/>.
    /// </remarks>
    public DateTime? CreatedAtUtc { get; set; }

    public static ApplicationUser Create(string firstName, string lastName, string email)
    {
        Validate(firstName, lastName);
        Guard.Against.NullOrWhiteSpace(email);

        return new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email
        };
    }

    public void UpdateDetails(string firstName, string lastName, string? phoneNumber)
    {
        Validate(firstName, lastName);

        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
    }

    /// <summary>
    /// Stamps everything that is true of an account exactly once, at the moment it is registered: when it began, and
    /// what it consented to.
    /// </summary>
    /// <remarks>
    /// One call rather than three, because there are two registration paths - the form and Google - and only one of them
    /// goes through <see cref="Create"/>; the external one builds the user directly. Consent has to be recorded on both,
    /// so hanging the creation stamp on the same call is what makes it impossible to add a third path that records
    /// consent and forgets when the account started.
    ///
    /// A later change of mind about marketing goes through <see cref="SetMarketingOptIn"/>, which deliberately touches
    /// neither the terms record nor the creation stamp.
    /// </remarks>
    public void RecordRegistration(bool marketingOptIn, DateTime nowUtc)
    {
        CreatedAtUtc = nowUtc;
        TermsAcceptedAtUtc = nowUtc;
        MarketingOptInAtUtc = marketingOptIn ? nowUtc : null;
    }

    // Changes the marketing opt-in after registration (e.g. from the account page). Stamps the opt-in
    // time when ticked and clears it when unticked. Kept separate from RecordRegistration so neither the
    // registration-only terms acceptance nor the creation stamp is touched here.
    public void SetMarketingOptIn(bool marketingOptIn, DateTime nowUtc)
    {
        MarketingOptInAtUtc = marketingOptIn ? nowUtc : null;
    }

    private static void Validate(string firstName, string lastName)
    {
        Guard.Against.NullOrWhiteSpace(firstName);
        Guard.Against.NullOrWhiteSpace(lastName);
    }
}
