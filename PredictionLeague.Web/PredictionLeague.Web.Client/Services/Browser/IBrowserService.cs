namespace PredictionLeague.Web.Client.Services.Browser;

public interface IBrowserService
{
    Task<bool> IsDesktop();
    Task<bool> IsTabletOrAbove();
}