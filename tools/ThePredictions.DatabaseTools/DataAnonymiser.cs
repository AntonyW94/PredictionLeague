using System.Dynamic;
using Bogus;

namespace ThePredictions.DatabaseTools;

public class DataAnonymiser
{
    public const string PreservedEmail = "antony.willson@hotmail.com";

    private const int Seed = 12345;

    public static IEnumerable<dynamic> AnonymiseUsers(IEnumerable<dynamic> users)
    {
        var faker = new Faker("en_GB")
        {
            Random = new Randomizer(Seed)
        };

        var anonymised = new List<dynamic>();
        var counter = 1;

        foreach (var user in users)
        {
            var dict = (IDictionary<string, object?>)user;
            var email = dict["Email"]?.ToString();

            if (string.Equals(email, PreservedEmail, StringComparison.OrdinalIgnoreCase))
            {
                anonymised.Add(user);
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

        Console.WriteLine($"[INFO] Anonymised {counter - 1} users (1 preserved)");
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
            result["EntryCode"] = GenerateRandomEntryCode(faker);

            anonymised.Add(result);
            counter++;
        }

        Console.WriteLine($"[INFO] Anonymised {counter - 1} leagues");
        return anonymised;
    }

    private static string GenerateRandomEntryCode(Faker faker)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        return new string(Enumerable.Range(0, 6).Select(_ => chars[faker.Random.Number(chars.Length - 1)]).ToArray());
    }
}
