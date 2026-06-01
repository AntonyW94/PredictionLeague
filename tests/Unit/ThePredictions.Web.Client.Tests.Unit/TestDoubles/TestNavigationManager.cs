using Microsoft.AspNetCore.Components;

namespace ThePredictions.Web.Client.Tests.Unit.TestDoubles;

/// <summary>Minimal <see cref="NavigationManager"/> that records navigations.</summary>
public sealed class TestNavigationManager : NavigationManager
{
    private const string DefaultBaseUri = "https://localhost/";

    public TestNavigationManager(string? currentUri = null)
    {
        Initialize(DefaultBaseUri, currentUri ?? DefaultBaseUri);
    }

    public string? LastNavigatedTo { get; private set; }

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        LastNavigatedTo = uri;
        Uri = ToAbsoluteUri(uri).ToString();
    }
}
