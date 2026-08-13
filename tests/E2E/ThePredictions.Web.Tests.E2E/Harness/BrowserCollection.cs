using Xunit;

namespace ThePredictions.Web.Tests.E2E.Harness;

/// <summary>
/// Every test in this assembly joins this collection, which shares the one browser and runs the tests
/// sequentially. Sequential is the right default here even though the contexts are isolated: the three test
/// accounts are fixed and shared, so two tests signed in as the same account at the same time would be
/// racing the same server-side session and refresh token.
/// </summary>
[CollectionDefinition(Name)]
public class BrowserCollection : ICollectionFixture<BrowserFixture>
{
    public const string Name = "Chromium browser";
}
