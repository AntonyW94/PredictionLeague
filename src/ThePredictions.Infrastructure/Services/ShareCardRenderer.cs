using Microsoft.Extensions.Logging;
using SkiaSharp;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Renders a prediction share card to a PNG using SkiaSharp. Team logos are fetched over HTTP
/// and, when one is missing or cannot be decoded, a coloured abbreviation badge is drawn instead
/// so the card always renders. The layout is a single branded column: header (brand logo, title,
/// subtitle), one row per match, footer. Light and dark colour schemes mirror the player's UI theme.
/// </summary>
public class ShareCardRenderer(HttpClient httpClient, ILogger<ShareCardRenderer> logger) : IShareCardRenderer
{
    private const int Width = 1080;
    private const int Padding = 60;
    private const int HeaderHeight = 260;
    private const int RowHeight = 104;
    private const int FooterHeight = 96;
    private const int LogoSize = 60;

    // Outcome colours read on both light and dark cards, so they are theme-independent.
    private static readonly SKColor ExactColour = SKColor.Parse("#00B960");
    private static readonly SKColor CorrectColour = SKColor.Parse("#CC8200");
    private static readonly SKColor IncorrectColour = SKColor.Parse("#E90052");
    private static readonly SKColor BadgeFill = SKColor.Parse("#5D3E85");

    // Brand logos are embedded resources (see the .csproj) so they are always present in a
    // framework-dependent publish. Decoded once and reused - SKImage is immutable and safe to share.
    private static readonly Lazy<SKImage?> DarkLogo = new(() => LoadLogo("logo-header-dark.png"));
    private static readonly Lazy<SKImage?> LightLogo = new(() => LoadLogo("logo-header-light.png"));

    public async Task<byte[]> RenderAsync(ShareCardModel model, CancellationToken cancellationToken)
    {
        var logos = await FetchLogosAsync(model.Matches, cancellationToken);
        var palette = BuildPalette(model.Theme);

        try
        {
            var height = HeaderHeight + (model.Matches.Count * RowHeight) + FooterHeight;

            using var surface = SKSurface.Create(new SKImageInfo(Width, height));
            var canvas = surface.Canvas;

            DrawBackground(canvas, height, palette);
            DrawHeader(canvas, model, palette);

            for (var i = 0; i < model.Matches.Count; i++)
            {
                var rowTop = HeaderHeight + (i * RowHeight);
                DrawMatchRow(canvas, model.Matches[i], rowTop, logos, palette);
            }

            DrawFooter(canvas, height, palette);

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

    private static Palette BuildPalette(ShareCardTheme theme)
    {
        return theme == ShareCardTheme.Dark
            ? new Palette(
                BackgroundTop: SKColor.Parse("#2C0A3D"),
                BackgroundBottom: SKColor.Parse("#3D195B"),
                Title: SKColor.Parse("#FFFFFF"),
                Subtitle: SKColor.Parse("#B9A6CC"),
                RowFill: new SKColor(255, 255, 255, 16),
                RowBorder: SKColor.Empty,
                Divider: new SKColor(255, 255, 255, 38),
                NeutralScore: SKColor.Parse("#FFFFFF"),
                Footer: SKColor.Parse("#FFFFFF"),
                Logo: DarkLogo.Value)
            : new Palette(
                BackgroundTop: SKColor.Parse("#F4EEFA"),
                BackgroundBottom: SKColor.Parse("#E9DEF4"),
                Title: SKColor.Parse("#2C0A3D"),
                Subtitle: SKColor.Parse("#6E5C86"),
                RowFill: SKColor.Parse("#FFFFFF"),
                RowBorder: SKColor.Parse("#E3D7F0"),
                Divider: SKColor.Parse("#D9CCEA"),
                NeutralScore: SKColor.Parse("#2C0A3D"),
                Footer: SKColor.Parse("#4A2E6C"),
                Logo: LightLogo.Value);
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

    private static SKImage? LoadLogo(string fileName)
    {
        var resourceName = $"ThePredictions.Infrastructure.Assets.{fileName}";
        using var stream = typeof(ShareCardRenderer).Assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
            return null;

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        using var data = SKData.CreateCopy(memory.ToArray());
        return SKImage.FromEncodedData(data);
    }

    private static void DrawBackground(SKCanvas canvas, int height, Palette palette)
    {
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            new SKColor[] { palette.BackgroundTop, palette.BackgroundBottom },
            null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint { Shader = shader, IsAntialias = true };
        canvas.DrawRect(SKRect.Create(0, 0, Width, height), paint);
    }

    private static void DrawHeader(SKCanvas canvas, ShareCardModel model, Palette palette)
    {
        var centreX = Width / 2f;

        if (palette.Logo is { } logo)
        {
            var targetHeight = 74f;
            var targetWidth = targetHeight * logo.Width / logo.Height;
            var maxWidth = Width - 260;

            if (targetWidth > maxWidth)
            {
                targetWidth = maxWidth;
                targetHeight = targetWidth * logo.Height / logo.Width;
            }

            var logoRect = SKRect.Create(centreX - (targetWidth / 2f), 44, targetWidth, targetHeight);
            canvas.DrawImage(logo, logoRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        var title = string.IsNullOrWhiteSpace(model.PlayerName)
            ? "My Predictions"
            : $"{model.PlayerName}'s Predictions";

        using var titleFont = CreateFont(58, bold: true);
        using var titlePaint = CreatePaint(palette.Title);
        DrawText(canvas, title, centreX, 168, titleFont, titlePaint, SKTextAlign.Center);

        using var subtitleFont = CreateFont(32, bold: false);
        using var subtitlePaint = CreatePaint(palette.Subtitle);
        DrawText(canvas, $"{model.SeasonName}  -  {model.RoundLabel}", centreX, 218, subtitleFont, subtitlePaint, SKTextAlign.Center);
    }

    private static void DrawMatchRow(
        SKCanvas canvas,
        ShareCardMatch match,
        float rowTop,
        IReadOnlyDictionary<string, SKImage?> logos,
        Palette palette)
    {
        var centreX = Width / 2f;
        var centreY = rowTop + (RowHeight / 2f);
        var rowRect = SKRect.Create(Padding, rowTop + 8, Width - (2 * Padding), RowHeight - 16);

        using var rowPaint = new SKPaint { Color = palette.RowFill, IsAntialias = true };
        canvas.DrawRoundRect(rowRect, 18, 18, rowPaint);

        if (palette.RowBorder != SKColor.Empty)
        {
            using var borderPaint = new SKPaint { Color = palette.RowBorder, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            canvas.DrawRoundRect(rowRect, 18, 18, borderPaint);
        }

        var homeLogoCentre = centreX - 150;
        var awayLogoCentre = centreX + 150;

        DrawTeamLogo(canvas, match.HomeTeamLogoUrl, match.HomeTeamAbbreviation, homeLogoCentre, centreY, logos);
        DrawTeamLogo(canvas, match.AwayTeamLogoUrl, match.AwayTeamAbbreviation, awayLogoCentre, centreY, logos);

        using var nameFont = CreateFont(30, bold: false);
        using var namePaint = CreatePaint(palette.Title);
        DrawText(canvas, match.HomeTeamShortName, homeLogoCentre - (LogoSize / 2f) - 22, centreY, nameFont, namePaint, SKTextAlign.Right);
        DrawText(canvas, match.AwayTeamShortName, awayLogoCentre + (LogoSize / 2f) + 22, centreY, nameFont, namePaint, SKTextAlign.Left);

        var scoreColour = match.IsScored ? OutcomeColour(match.Outcome) : palette.NeutralScore;
        var scoreY = match.IsScored ? centreY - 12 : centreY;

        using var scoreFont = CreateFont(46, bold: true);
        using var scorePaint = CreatePaint(scoreColour);
        DrawText(canvas, $"{match.PredictedHomeScore} - {match.PredictedAwayScore}", centreX, scoreY, scoreFont, scorePaint, SKTextAlign.Center);

        if (match.IsScored)
        {
            using var actualFont = CreateFont(24, bold: false);
            using var actualPaint = CreatePaint(palette.Subtitle);
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
        using var abbreviationPaint = CreatePaint(SKColor.Parse("#FFFFFF"));
        DrawText(canvas, abbreviation, centreX, centreY, abbreviationFont, abbreviationPaint, SKTextAlign.Center);
    }

    private static void DrawFooter(SKCanvas canvas, int height, Palette palette)
    {
        var centreX = Width / 2f;
        var footerTop = height - FooterHeight;

        using var dividerPaint = new SKPaint { Color = palette.Divider, IsAntialias = true, StrokeWidth = 2 };
        canvas.DrawLine(Padding, footerTop, Width - Padding, footerTop, dividerPaint);

        using var footerFont = CreateFont(30, bold: true);
        using var footerPaint = CreatePaint(palette.Footer);
        DrawText(canvas, "thepredictions.co.uk", centreX, footerTop + (FooterHeight / 2f), footerFont, footerPaint, SKTextAlign.Center);
    }

    private static SKColor OutcomeColour(PredictionOutcome outcome)
    {
        return outcome switch
        {
            PredictionOutcome.ExactScore => ExactColour,
            PredictionOutcome.CorrectResult => CorrectColour,
            PredictionOutcome.Incorrect => IncorrectColour,
            _ => ExactColour
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

    private sealed record Palette(
        SKColor BackgroundTop,
        SKColor BackgroundBottom,
        SKColor Title,
        SKColor Subtitle,
        SKColor RowFill,
        SKColor RowBorder,
        SKColor Divider,
        SKColor NeutralScore,
        SKColor Footer,
        SKImage? Logo);
}
