using System.Dynamic;
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

    public static IEnumerable<dynamic> AnonymiseUsers(IEnumerable<dynamic> users)
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

            var firstName = faker.Name.FirstName();
            var lastName = faker.Name.LastName();
            var fakeEmail = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{counter}@testmail.com";

            result["Email"] = fakeEmail;
            result["NormalizedEmail"] = fakeEmail.ToUpperInvariant();
            result["UserName"] = fakeEmail;
            result["NormalizedUserName"] = fakeEmail.ToUpperInvariant();
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

        Console.WriteLine($"[INFO] Anonymised {counter - 1} users ({preservedCount} preserved)");
        return anonymised;
    }

    public static IEnumerable<dynamic> AnonymiseLeagues(IEnumerable<dynamic> leagues)
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

            var surname = faker.Name.LastName();
            var price = Convert.ToDecimal(dict["Price"]);
            var isFree = price == 0m;

            result["Name"] = isFree ? $"{surname}'s Free League" : $"{surname}'s League";

            // Only randomise real join codes; public leagues have no code (NULL) and must stay public.
            result["EntryCode"] = dict["EntryCode"] is null ? null : GenerateRandomEntryCode(faker);

            // Scrub peer-to-peer bank details - real (encrypted) bank info must never reach a dev copy.
            result["BankAccountName"] = null;
            result["BankSortCode"] = null;
            result["BankAccountNumber"] = null;

            anonymised.Add(result);
            counter++;
        }

        Console.WriteLine($"[INFO] Anonymised {counter - 1} leagues");
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
