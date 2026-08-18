using System.Dynamic;
using System.Globalization;
using System.Text;
using Bogus;

namespace ThePredictions.DatabaseTools;

public class DataAnonymiser
{
    public static readonly string[] PreservedEmails =
    [
        "antony.willson@hotmail.com",
        "joelgra95@gmail.com"
    ];

    private const int Seed = 12345;

    public static IEnumerable<dynamic> AnonymiseUsers(IEnumerable<dynamic> users, bool preserveFirstNames = false)
    {
        var faker = new Faker("en_GB")
        {
            Random = new Randomizer(Seed)
        };

        var anonymised = new List<dynamic>();
        var counter = 1;
        var preservedCount = 0;

        foreach (var user in users)
        {
            var dict = (IDictionary<string, object?>)user;
            var email = dict["Email"]?.ToString();

            if (email is not null && PreservedEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
            {
                anonymised.Add(user);
                preservedCount++;
                continue;
            }

            IDictionary<string, object?> result = new ExpandoObject();

            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }

            var realFirstName = preserveFirstNames
                ? (dict.TryGetValue("FirstName", out var existingFirstName) ? (existingFirstName as string)?.Trim() : null)
                : null;

            // A preserved first name is whatever somebody typed into the registration form, so it can arrive with
            // surrounding space, a space in the middle, an apostrophe or an accent. Falling back when nothing usable
            // survives is what stops an address beginning with a bare dot.
            var firstName = string.IsNullOrEmpty(EmailNamePart(realFirstName))
                ? faker.Name.FirstName()
                : realFirstName!;

            var lastName = faker.Name.LastName();
            var fakeEmail = $"{EmailNamePart(firstName)}.{EmailNamePart(lastName)}{counter}@testmail.com";

            result["Email"] = fakeEmail;
            result["NormalizedEmail"] = fakeEmail.ToUpperInvariant();
            result["UserName"] = fakeEmail;
            result["NormalizedUserName"] = fakeEmail.ToUpperInvariant();
            // Trimmed, because the name is displayed as well as emailed - an untrimmed one renders as a double space
            // between the forename and surname wherever the two are composed.
            result["FirstName"] = firstName;
            result["LastName"] = lastName;
            result["PasswordHash"] = "INVALIDATED";
            result["SecurityStamp"] = Guid.NewGuid().ToString();
            result["PhoneNumber"] = null;
            result["PhoneNumberConfirmed"] = false;
            result["TwoFactorEnabled"] = false;
            result["LockoutEnd"] = null;
            result["AccessFailedCount"] = 0;

            anonymised.Add(result);
            counter++;
        }

        var firstNameMode = preserveFirstNames ? "first names preserved, last names only" : "first and last names";
        Console.WriteLine($"[INFO] Anonymised {counter - 1} users ({preservedCount} preserved, {firstNameMode})");
        return anonymised;
    }

    /// <summary>
    /// A name reduced to what is safe to put in the local part of an email address.
    /// </summary>
    /// <remarks>
    /// Unaccented ASCII letters and digits only, lowercased. Everything else - space, apostrophe, hyphen, accent - is
    /// dropped rather than replaced, so "O&#39;Kon" becomes "okon" and "Mary Jane" becomes "maryjane".
    ///
    /// A generated name never needs this. A <b>preserved</b> one does, because it comes from production untouched: three
    /// dev accounts ended up with addresses like <c>ben .rogahn28@testmail.com</c> from first names carrying a trailing
    /// space. Bogus can also produce surnames with an apostrophe, so it is applied to both halves rather than only the
    /// half that has bitten so far.
    ///
    /// Returns an empty string when nothing survives, which the caller treats as "no usable name" - an address must not
    /// begin with a dot.
    /// </remarks>
    internal static string EmailNamePart(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalised = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalised.Length);

        foreach (var character in normalised)
        {
            // Decomposing first turns an accented letter into its base letter plus a combining mark, so dropping the
            // marks keeps the letter rather than losing the whole character.
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsAsciiLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static IEnumerable<dynamic> AnonymiseLeagues(IEnumerable<dynamic> leagues, bool keepLeagueNames = false)
    {
        var faker = new Faker("en_GB")
        {
            Random = new Randomizer(Seed + 1)
        };

        var anonymised = new List<dynamic>();
        var counter = 1;

        foreach (var league in leagues)
        {
            var dict = (IDictionary<string, object?>)league;
            IDictionary<string, object?> result = new ExpandoObject();

            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }

            // League names can optionally be kept as-is (e.g. to make a dev copy easier to navigate);
            // the sensitive fields below (bank details, join codes) are still scrubbed either way.
            if (!keepLeagueNames)
            {
                var surname = faker.Name.LastName();
                var price = Convert.ToDecimal(dict["Price"]);
                var isFree = price == 0m;

                result["Name"] = isFree ? $"{surname}'s Free League" : $"{surname}'s League";
            }

            // Only randomise real join codes; public leagues have no code (NULL) and must stay public.
            result["EntryCode"] = dict["EntryCode"] is null ? null : GenerateRandomEntryCode(faker);

            // Scrub peer-to-peer bank details - real (encrypted) bank info must never reach a dev copy.
            result["BankAccountName"] = null;
            result["BankSortCode"] = null;
            result["BankAccountNumber"] = null;

            anonymised.Add(result);
            counter++;
        }

        var nameMode = keepLeagueNames ? "names kept" : "names anonymised";
        Console.WriteLine($"[INFO] Anonymised {counter - 1} leagues ({nameMode})");
        return anonymised;
    }

    public static IEnumerable<dynamic> AnonymiseSeasonPasses(IEnumerable<dynamic> seasonPasses)
    {
        var anonymised = new List<dynamic>();
        var counter = 0;

        foreach (var seasonPass in seasonPasses)
        {
            var dict = (IDictionary<string, object?>)seasonPass;
            IDictionary<string, object?> result = new ExpandoObject();

            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Strip the real Stripe payment reference; it must never reach a dev copy.
            result["StripePaymentReference"] = null;

            anonymised.Add(result);
            counter++;
        }

        Console.WriteLine($"[INFO] Anonymised {counter} season passes");
        return anonymised;
    }

    public static IEnumerable<dynamic> AnonymiseUserPayoutDetails(IEnumerable<dynamic> payoutDetails)
    {
        var anonymised = new List<dynamic>();
        var counter = 0;

        foreach (var detail in payoutDetails)
        {
            var dict = (IDictionary<string, object?>)detail;
            IDictionary<string, object?> result = new ExpandoObject();

            foreach (var kvp in dict)
            {
                result[kvp.Key] = kvp.Value;
            }

            // Scrub the encrypted payout bank details - real (encrypted) details must never reach a dev copy.
            result["AccountName"] = null;
            result["SortCode"] = null;
            result["AccountNumber"] = null;

            anonymised.Add(result);
            counter++;
        }

        Console.WriteLine($"[INFO] Anonymised {counter} user payout details");
        return anonymised;
    }

    private static string GenerateRandomEntryCode(Faker faker)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        return new string(Enumerable.Range(0, 6).Select(_ => chars[faker.Random.Number(chars.Length - 1)]).ToArray());
    }
}
