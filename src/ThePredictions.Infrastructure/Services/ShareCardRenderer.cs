using Microsoft.Extensions.Logging;
using SkiaSharp;
using Svg.Skia;
using ThePredictions.Application.Features.Sharing.Models;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Infrastructure.Services;

/// <summary>
/// Renders a prediction share card to a PNG using SkiaSharp. Team logos are fetched over HTTP
/// (raster or SVG - the football data stores badges and flags as SVG) and, when one is missing or
/// cannot be decoded, a coloured abbreviation badge is drawn instead so the card always renders.
/// Each match is a full-width row: the home team on the left, the away team on the right, and the
/// prediction in the centre as an outcome-coloured pill mirroring the site's prediction badges.
/// Light and dark colour schemes mirror the player's UI theme.
/// </summary>
public class ShareCardRenderer(HttpClient httpClient, ILogger<ShareCardRenderer> logger) : IShareCardRenderer
{
    private const int Width = 1080;
    private const int Padding = 60;
    private const int HeaderHeight = 320;
    private const int RowHeight = 128;
    private const int BottomPadding = 56;
    private const int LogoSize = 64;
    private const int SvgRasterSize = 128;

    // Outcome colours mirror the site's prediction badges: green (exact), amber (correct result -
    // a yellow-tinted pill with orange disc/text), red (incorrect). Theme-independent.
    private static readonly SKColor ExactColour = SKColor.Parse("#00B960");
    private static readonly SKColor CorrectDisc = SKColor.Parse("#CC8200");
    private static readonly SKColor CorrectTint = SKColor.Parse("#EBFF01");
    private static readonly SKColor IncorrectColour = SKColor.Parse("#E90052");
    private static readonly SKColor White = SKColor.Parse("#FFFFFF");
    private static readonly SKColor AbbreviationBadgeFill = SKColor.Parse("#5D3E85");

    private static readonly Lazy<SKImage?> DarkLogo = new(() => LoadEmbeddedLogo("logo-header-dark.png"));
    private static readonly Lazy<SKImage?> LightLogo = new(() => LoadEmbeddedLogo("logo-header-light.png"));

    private enum OutcomeGlyph { None, Tick, Cross }

    public async Task<byte[]> RenderAsync(ShareCardModel model, CancellationToken cancellationToken)
    {
        var logos = await FetchLogosAsync(model.Matches, cancellationToken);
        var palette = BuildPalette(model.Theme);

        try
        {
            var height = HeaderHeight + (model.Matches.Count * RowHeight) + BottomPadding;

            using var surface = SKSurface.Create(new SKImageInfo(Width, height));
            var canvas = surface.Canvas;

            DrawBackground(canvas, height, palette);
            DrawHeader(canvas, model, palette);

            for (var i = 0; i < model.Matches.Count; i++)
            {
                var rowTop = HeaderHeight + (i * RowHeight);
                DrawMatchRow(canvas, model.Matches[i], rowTop, logos, palette);
            }

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
                Title: White,
                Subtitle: SKColor.Parse("#B9A6CC"),
                RowFill: new SKColor(255, 255, 255, 16),
                RowBorder: SKColor.Empty,
                NeutralPillFill: new SKColor(255, 255, 255, 28),
                NeutralScore: White,
                CorrectTintBase: CorrectDisc,
                TintAlpha: 60,
                ScoreTextWhite: true,
                Logo: DarkLogo.Value)
            : new Palette(
                BackgroundTop: SKColor.Parse("#F4EEFA"),
                BackgroundBottom: SKColor.Parse("#E9DEF4"),
                Title: SKColor.Parse("#2C0A3D"),
                Subtitle: SKColor.Parse("#6E5C86"),
                RowFill: White,
                RowBorder: SKColor.Parse("#E3D7F0"),
                NeutralPillFill: SKColor.Parse("#EFE7F7"),
                NeutralScore: SKColor.Parse("#2C0A3D"),
                CorrectTintBase: CorrectTint,
                TintAlpha: 40,
                ScoreTextWhite: false,
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
            return DecodeLogo(bytes);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Could not fetch team logo for share card from {LogoUrl}", url);
            return null;
        }
    }

    // Team logos are a mix of raster (PNG) and SVG (Premier League badges, circle-flags), so sniff
    // the payload and rasterise SVG - SkiaSharp decodes raster only.
    private static SKImage? DecodeLogo(byte[] bytes)
    {
        if (LooksLikeSvg(bytes))
            return RasteriseSvg(bytes);

        using var data = SKData.CreateCopy(bytes);
        return SKImage.FromEncodedData(data);
    }

    private static bool LooksLikeSvg(byte[] bytes)
    {
        var index = 0;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            index = 3;

        while (index < bytes.Length && bytes[index] is 0x20 or 0x09 or 0x0A or 0x0D)
            index++;

        return index < bytes.Length && bytes[index] == (byte)'<';
    }

    private static SKImage? RasteriseSvg(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var svg = new SKSvg();
        var picture = svg.Load(stream);

        var bounds = picture?.CullRect ?? SKRect.Empty;
        if (picture is null || bounds.Width <= 0 || bounds.Height <= 0)
            return null;

        var scale = Math.Min(SvgRasterSize / bounds.Width, SvgRasterSize / bounds.Height);
        var drawWidth = bounds.Width * scale;
        var drawHeight = bounds.Height * scale;

        using var surface = SKSurface.Create(new SKImageInfo(SvgRasterSize, SvgRasterSize));
        surface.Canvas.Clear(SKColors.Transparent);

        var matrix = SKMatrix.CreateScaleTranslation(
            scale,
            scale,
            ((SvgRasterSize - drawWidth) / 2f) - (bounds.Left * scale),
            ((SvgRasterSize - drawHeight) / 2f) - (bounds.Top * scale));

        surface.Canvas.DrawPicture(picture, in matrix);
        surface.Canvas.Flush();
        return surface.Snapshot();
    }

    private static SKImage? LoadEmbeddedLogo(string fileName)
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
            var targetHeight = 84f;
            var targetWidth = targetHeight * logo.Width / logo.Height;
            var maxWidth = Width - 300;

            if (targetWidth > maxWidth)
            {
                targetWidth = maxWidth;
                targetHeight = targetWidth * logo.Height / logo.Width;
            }

            var logoRect = SKRect.Create(centreX - (targetWidth / 2f), 58, targetWidth, targetHeight);
            canvas.DrawImage(logo, logoRect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        using var titleFont = CreateFont(58, bold: true);
        using var titlePaint = CreatePaint(palette.Title);
        var title = string.IsNullOrWhiteSpace(model.PlayerName) ? "My Predictions" : $"{model.PlayerName}'s Predictions";
        DrawText(canvas, title, centreX, 210, titleFont, titlePaint, SKTextAlign.Center);

        using var subtitleFont = CreateFont(31, bold: false);
        using var subtitlePaint = CreatePaint(palette.Subtitle);
        DrawText(canvas, $"{model.SeasonName}  -  {model.RoundLabel}", centreX, 270, subtitleFont, subtitlePaint, SKTextAlign.Center);
    }

    private static void DrawMatchRow(
        SKCanvas canvas,
        ShareCardMatch match,
        float rowTop,
        IReadOnlyDictionary<string, SKImage?> logos,
        Palette palette)
    {
        var centreY = rowTop + (RowHeight / 2f);
        var rowRect = SKRect.Create(Padding, rowTop + 8, Width - (2 * Padding), RowHeight - 16);

        using (var rowPaint = new SKPaint { Color = palette.RowFill, IsAntialias = true })
            canvas.DrawRoundRect(rowRect, 20, 20, rowPaint);

        if (palette.RowBorder != SKColor.Empty)
        {
            using var borderPaint = new SKPaint { Color = palette.RowBorder, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2 };
            canvas.DrawRoundRect(rowRect, 20, 20, borderPaint);
        }

        // Teams sit a comfortable inset from the row edges (not hard against them), which also pulls
        // each team closer to the central score so the row reads as one unit.
        var homeLogoCentre = Padding + 66;
        var awayLogoCentre = Width - Padding - 66;

        DrawTeamLogo(canvas, match.HomeTeamLogoUrl, match.HomeTeamAbbreviation, homeLogoCentre, centreY, logos);
        DrawTeamLogo(canvas, match.AwayTeamLogoUrl, match.AwayTeamAbbreviation, awayLogoCentre, centreY, logos);

        using var nameFont = CreateFont(29, bold: true);
        using var namePaint = CreatePaint(palette.Title);
        DrawText(canvas, match.HomeTeamShortName, homeLogoCentre + (LogoSize / 2f) + 18, centreY, nameFont, namePaint, SKTextAlign.Left);
        DrawText(canvas, match.AwayTeamShortName, awayLogoCentre - (LogoSize / 2f) - 18, centreY, nameFont, namePaint, SKTextAlign.Right);

        DrawScoreBadge(canvas, match, Width / 2f, centreY, palette);
    }

    private static void DrawScoreBadge(SKCanvas canvas, ShareCardMatch match, float centreX, float centreY, Palette palette)
    {
        const float pillHeight = 58f;
        var radius = pillHeight / 2f;
        var scoreText = $"{match.PredictedHomeScore} - {match.PredictedAwayScore}";

        using var scoreFont = CreateFont(34, bold: true);
        var textWidth = scoreFont.MeasureText(scoreText);
        var pillCentreY = match.IsScored ? centreY - 16 : centreY;

        if (match.IsScored)
        {
            var (accent, tint, glyph) = OutcomeVisual(match.Outcome, palette.TintAlpha, palette.CorrectTintBase);
            var scoreColour = palette.ScoreTextWhite ? White : accent;

            const float gap = 12f;
            const float rightPad = 28f;
            var pillWidth = pillHeight + gap + textWidth + rightPad;
            var left = centreX - (pillWidth / 2f);
            var pillRect = SKRect.Create(left, pillCentreY - (pillHeight / 2f), pillWidth, pillHeight);

            using (var pillPaint = new SKPaint { Color = tint, IsAntialias = true })
                canvas.DrawRoundRect(pillRect, radius, radius, pillPaint);

            var discCentre = new SKPoint(left + (pillHeight / 2f), pillCentreY);
            using (var discPaint = new SKPaint { Color = accent, IsAntialias = true })
                canvas.DrawCircle(discCentre, pillHeight / 2f, discPaint);

            DrawGlyph(canvas, glyph, discCentre, pillHeight * 0.24f);

            // Centre the score in the space between the disc and the pill's right padding.
            var textCentre = (left + pillHeight + gap + (left + pillWidth - rightPad)) / 2f;
            using var scorePaint = CreatePaint(scoreColour);
            DrawText(canvas, scoreText, textCentre, pillCentreY, scoreFont, scorePaint, SKTextAlign.Center);

            using var actualFont = CreateFont(23, bold: false);
            using var actualPaint = CreatePaint(palette.Subtitle);
            DrawText(canvas, $"FT {match.ActualHomeScore}-{match.ActualAwayScore}", centreX, centreY + 33, actualFont, actualPaint, SKTextAlign.Center);
        }
        else
        {
            const float sidePad = 30f;
            var pillWidth = textWidth + (2 * sidePad);
            var pillRect = SKRect.Create(centreX - (pillWidth / 2f), pillCentreY - (pillHeight / 2f), pillWidth, pillHeight);

            using (var pillPaint = new SKPaint { Color = palette.NeutralPillFill, IsAntialias = true })
                canvas.DrawRoundRect(pillRect, radius, radius, pillPaint);

            using var scorePaint = CreatePaint(palette.NeutralScore);
            DrawText(canvas, scoreText, centreX, pillCentreY, scoreFont, scorePaint, SKTextAlign.Center);
        }
    }

    // Returns the solid accent colour (icon disc + light-theme score text), the translucent pill
    // tint, and which glyph to stamp in the disc, for a scored prediction.
    private static (SKColor Accent, SKColor Tint, OutcomeGlyph Glyph) OutcomeVisual(PredictionOutcome outcome, byte tintAlpha, SKColor correctTintBase)
    {
        return outcome switch
        {
            PredictionOutcome.ExactScore => (ExactColour, ExactColour.WithAlpha(tintAlpha), OutcomeGlyph.Tick),
            PredictionOutcome.CorrectResult => (CorrectDisc, correctTintBase.WithAlpha(tintAlpha), OutcomeGlyph.Tick),
            PredictionOutcome.Incorrect => (IncorrectColour, IncorrectColour.WithAlpha(tintAlpha), OutcomeGlyph.Cross),
            _ => (ExactColour, ExactColour.WithAlpha(tintAlpha), OutcomeGlyph.None)
        };
    }

    // A white tick or cross centred in the icon disc, drawn as stroked paths so it scales cleanly.
    private static void DrawGlyph(SKCanvas canvas, OutcomeGlyph glyph, SKPoint centre, float size)
    {
        if (glyph == OutcomeGlyph.None)
            return;

        using var paint = new SKPaint
        {
            Color = White,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = size * 0.42f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        if (glyph == OutcomeGlyph.Tick)
        {
            canvas.DrawLine(centre.X - (size * 1.05f), centre.Y + (size * 0.05f), centre.X - (size * 0.30f), centre.Y + (size * 0.75f), paint);
            canvas.DrawLine(centre.X - (size * 0.30f), centre.Y + (size * 0.75f), centre.X + (size * 1.05f), centre.Y - (size * 0.75f), paint);
        }
        else
        {
            canvas.DrawLine(centre.X - (size * 0.8f), centre.Y - (size * 0.8f), centre.X + (size * 0.8f), centre.Y + (size * 0.8f), paint);
            canvas.DrawLine(centre.X - (size * 0.8f), centre.Y + (size * 0.8f), centre.X + (size * 0.8f), centre.Y - (size * 0.8f), paint);
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

        using var badgePaint = new SKPaint { Color = AbbreviationBadgeFill, IsAntialias = true };
        canvas.DrawCircle(centreX, centreY, LogoSize / 2f, badgePaint);

        using var abbreviationFont = CreateFont(22, bold: true);
        using var abbreviationPaint = CreatePaint(White);
        DrawText(canvas, abbreviation, centreX, centreY, abbreviationFont, abbreviationPaint, SKTextAlign.Center);
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
        SKColor NeutralPillFill,
        SKColor NeutralScore,
        SKColor CorrectTintBase,
        byte TintAlpha,
        bool ScoreTextWhite,
        SKImage? Logo);
}
