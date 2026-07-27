using Microsoft.Extensions.Logging;
using SkiaSharp;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Renders a prediction share card to a PNG using SkiaSharp. Team logos are fetched over HTTP
/// and, when one is missing or cannot be decoded, a coloured abbreviation badge is drawn instead
/// so the card always renders. The layout is a single branded column: header, one row per match,
/// footer.
/// </summary>
public class ShareCardRenderer(HttpClient httpClient, ILogger<ShareCardRenderer> logger) : IShareCardRenderer
{
    private const int Width = 1080;
    private const int Padding = 60;
    private const int HeaderHeight = 260;
    private const int RowHeight = 104;
    private const int FooterHeight = 96;
    private const int LogoSize = 60;

    private static readonly SKColor BackgroundTop = SKColor.Parse("#2C0A3D");
    private static readonly SKColor BackgroundBottom = SKColor.Parse("#3D195B");
    private static readonly SKColor White = SKColor.Parse("#FFFFFF");
    private static readonly SKColor Muted = SKColor.Parse("#B9A6CC");
    private static readonly SKColor RowFill = new(255, 255, 255, 16);
    private static readonly SKColor Divider = new(255, 255, 255, 38);
    private static readonly SKColor BadgeFill = SKColor.Parse("#5D3E85");
    private static readonly SKColor ExactColour = SKColor.Parse("#00B960");
    private static readonly SKColor CorrectColour = SKColor.Parse("#CC8200");
    private static readonly SKColor IncorrectColour = SKColor.Parse("#E90052");

    public async Task<byte[]> RenderAsync(ShareCardModel model, CancellationToken cancellationToken)
    {
        var logos = await FetchLogosAsync(model.Matches, cancellationToken);

        try
        {
            var height = HeaderHeight + (model.Matches.Count * RowHeight) + FooterHeight;

            using var surface = SKSurface.Create(new SKImageInfo(Width, height));
            var canvas = surface.Canvas;

            DrawBackground(canvas, height);
            DrawHeader(canvas, model);

            for (var i = 0; i < model.Matches.Count; i++)
            {
                var rowTop = HeaderHeight + (i * RowHeight);
                DrawMatchRow(canvas, model.Matches[i], rowTop, logos);
            }

            DrawFooter(canvas, height);

            using var image = surface.Snapshot();
            using var data = image.Encode();
            return data.ToArray();
        }
        finally
        {
            foreach (var logo in logos.Values)
                logo?.Dispose();
        }
    }

    private async Task<Dictionary<string, SKImage?>> FetchLogosAsync(
        IReadOnlyList<ShareCardMatch> matches,
        CancellationToken cancellationToken)
    {
        var urls = matches
            .SelectMany(m => new[] { m.HomeTeamLogoUrl, m.AwayTeamLogoUrl })
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct()
            .ToList();

        var pairs = await Task.WhenAll(urls.Select(async url =>
        {
            var image = await FetchLogoAsync(url, cancellationToken);
            return (url, image);
        }));

        return pairs.ToDictionary(p => p.url, p => p.image);
    }

    private async Task<SKImage?> FetchLogoAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            return null;

        try
        {
            var bytes = await httpClient.GetByteArrayAsync(url, cancellationToken);
            using var data = SKData.CreateCopy(bytes);
            return SKImage.FromEncodedData(data);
        }
        catch (Exception exception)
        {
            // A missing or undecodable logo (network error, SVG, 404) is non-fatal: the card falls
            // back to an abbreviation badge, so log at debug and carry on.
            logger.LogDebug(exception, "Could not fetch team logo for share card from {LogoUrl}", url);
            return null;
        }
    }

    private static void DrawBackground(SKCanvas canvas, int height)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            new SKColor[] { BackgroundTop, BackgroundBottom },
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawRect(SKRect.Create(0, 0, Width, height), paint);
    }

    private static void DrawHeader(SKCanvas canvas, ShareCardModel model)
    {
        var centreX = Width / 2f;

        using var brandFont = CreateFont(38, bold: true);
        using var brandPaint = CreatePaint(White);
        DrawText(canvas, "THE PREDICTIONS", centreX, 62, brandFont, brandPaint, SKTextAlign.Center);

        using var accentPaint = new SKPaint { Color = ExactColour, IsAntialias = true };
        canvas.DrawRoundRect(SKRect.Create(centreX - 44, 86, 88, 6), 3, 3, accentPaint);

        var title = string.IsNullOrWhiteSpace(model.PlayerName)
            ? "My Predictions"
            : $"{model.PlayerName}'s Predictions";

        using var titleFont = CreateFont(58, bold: true);
        using var titlePaint = CreatePaint(White);
        DrawText(canvas, title, centreX, 158, titleFont, titlePaint, SKTextAlign.Center);

        using var subtitleFont = CreateFont(32, bold: false);
        using var subtitlePaint = CreatePaint(Muted);
        DrawText(canvas, $"{model.SeasonName}  -  {model.RoundLabel}", centreX, 212, subtitleFont, subtitlePaint, SKTextAlign.Center);
    }

    private static void DrawMatchRow(
        SKCanvas canvas,
        ShareCardMatch match,
        float rowTop,
        IReadOnlyDictionary<string, SKImage?> logos)
    {
        var centreX = Width / 2f;
        var centreY = rowTop + (RowHeight / 2f);

        using var rowPaint = new SKPaint { Color = RowFill, IsAntialias = true };
        canvas.DrawRoundRect(SKRect.Create(Padding, rowTop + 8, Width - (2 * Padding), RowHeight - 16), 18, 18, rowPaint);

        var homeLogoCentre = centreX - 150;
        var awayLogoCentre = centreX + 150;

        DrawTeamLogo(canvas, match.HomeTeamLogoUrl, match.HomeTeamAbbreviation, homeLogoCentre, centreY, logos);
        DrawTeamLogo(canvas, match.AwayTeamLogoUrl, match.AwayTeamAbbreviation, awayLogoCentre, centreY, logos);

        using var nameFont = CreateFont(30, bold: false);
        using var namePaint = CreatePaint(White);
        DrawText(canvas, match.HomeTeamShortName, homeLogoCentre - (LogoSize / 2f) - 22, centreY, nameFont, namePaint, SKTextAlign.Right);
        DrawText(canvas, match.AwayTeamShortName, awayLogoCentre + (LogoSize / 2f) + 22, centreY, nameFont, namePaint, SKTextAlign.Left);

        var scoreColour = match.IsScored ? OutcomeColour(match.Outcome) : White;
        var scoreY = match.IsScored ? centreY - 12 : centreY;

        using var scoreFont = CreateFont(46, bold: true);
        using var scorePaint = CreatePaint(scoreColour);
        DrawText(canvas, $"{match.PredictedHomeScore} - {match.PredictedAwayScore}", centreX, scoreY, scoreFont, scorePaint, SKTextAlign.Center);

        if (match.IsScored)
        {
            using var actualFont = CreateFont(24, bold: false);
            using var actualPaint = CreatePaint(Muted);
            DrawText(canvas, $"FT {match.ActualHomeScore}-{match.ActualAwayScore}", centreX, centreY + 26, actualFont, actualPaint, SKTextAlign.Center);
        }
    }

    private static void DrawTeamLogo(
        SKCanvas canvas,
        string? logoUrl,
        string abbreviation,
        float centreX,
        float centreY,
        IReadOnlyDictionary<string, SKImage?> logos)
    {
        SKImage? image = null;
        if (!string.IsNullOrWhiteSpace(logoUrl))
            logos.TryGetValue(logoUrl, out image);

        if (image is not null)
        {
            var destination = SKRect.Create(centreX - (LogoSize / 2f), centreY - (LogoSize / 2f), LogoSize, LogoSize);
            canvas.DrawImage(image, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Nearest));
            return;
        }

        using var badgePaint = new SKPaint { Color = BadgeFill, IsAntialias = true };
        canvas.DrawCircle(centreX, centreY, LogoSize / 2f, badgePaint);

        using var abbreviationFont = CreateFont(22, bold: true);
        using var abbreviationPaint = CreatePaint(White);
        DrawText(canvas, abbreviation, centreX, centreY, abbreviationFont, abbreviationPaint, SKTextAlign.Center);
    }

    private static void DrawFooter(SKCanvas canvas, int height)
    {
        var centreX = Width / 2f;
        var footerTop = height - FooterHeight;

        using var dividerPaint = new SKPaint { Color = Divider, IsAntialias = true, StrokeWidth = 2 };
        canvas.DrawLine(Padding, footerTop, Width - Padding, footerTop, dividerPaint);

        using var footerFont = CreateFont(30, bold: true);
        using var footerPaint = CreatePaint(White);
        DrawText(canvas, "thepredictions.co.uk", centreX, footerTop + (FooterHeight / 2f), footerFont, footerPaint, SKTextAlign.Center);
    }

    private static SKColor OutcomeColour(PredictionOutcome outcome)
    {
        return outcome switch
        {
            PredictionOutcome.ExactScore => ExactColour,
            PredictionOutcome.CorrectResult => CorrectColour,
            PredictionOutcome.Incorrect => IncorrectColour,
            _ => White
        };
    }

    private static SKFont CreateFont(float size, bool bold)
    {
        var typeface = SKTypeface.FromFamilyName(
            "Arial",
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);

        return new SKFont(typeface, size)
        {
            Edging = SKFontEdging.SubpixelAntialias,
            Subpixel = true
        };
    }

    private static SKPaint CreatePaint(SKColor colour) => new() { Color = colour, IsAntialias = true };

    private static void DrawText(SKCanvas canvas, string text, float x, float centreY, SKFont font, SKPaint paint, SKTextAlign align)
    {
        var metrics = font.Metrics;
        var baselineY = centreY - ((metrics.Ascent + metrics.Descent) / 2f);
        canvas.DrawText(text, x, baselineY, align, font, paint);
    }
}
