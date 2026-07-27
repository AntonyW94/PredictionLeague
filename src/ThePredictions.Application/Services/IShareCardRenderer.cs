using ThePredictions.Application.Features.Sharing.Models;

namespace ThePredictions.Application.Services;

/// <summary>
/// Renders a player's prediction share card to a PNG image. The implementation fetches team
/// logos and composes a branded card server-side, so the output is identical on every device
/// and free of the tainted-canvas limitation of client-side capture.
/// </summary>
public interface IShareCardRenderer
{
    Task<byte[]> RenderAsync(ShareCardModel model, CancellationToken cancellationToken);
}
