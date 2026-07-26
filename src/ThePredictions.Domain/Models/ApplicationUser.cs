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

    public void RecordRegistrationConsent(bool marketingOptIn, DateTime nowUtc)
    {
        TermsAcceptedAtUtc = nowUtc;
        MarketingOptInAtUtc = marketingOptIn ? nowUtc : null;
    }

    // Changes the marketing opt-in after registration (e.g. from the account page). Stamps the opt-in
    // time when ticked and clears it when unticked. Kept separate from RecordRegistrationConsent so the
    // registration-only terms acceptance is not touched here.
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
