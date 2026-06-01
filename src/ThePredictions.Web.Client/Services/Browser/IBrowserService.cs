namespace ThePredictions.Web.Client.Services.Browser;

public interface IBrowserService
{
    Task<bool> IsTabletOrAbove();

    Task<bool> IsDesktop();
}