using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Contracts.Badges;

/// <summary>Stable badge keys, shared by the server catalogue, the evaluator and the client.</summary>
[ExcludeFromCodeCoverage]
public static class BadgeKeys
{
    // Collections (levelled)
    public const string Marksman1 = "marksman-1";
    public const string Marksman2 = "marksman-2";
    public const string Marksman3 = "marksman-3";
    public const string Sharpshooter1 = "sharpshooter-1";
    public const string Sharpshooter2 = "sharpshooter-2";
    public const string Sharpshooter3 = "sharpshooter-3";
    public const string OnFire1 = "on-fire-1";
    public const string OnFire2 = "on-fire-2";
    public const string OnFire3 = "on-fire-3";
    public const string Socialite1 = "socialite-1";
    public const string Socialite2 = "socialite-2";
    public const string Socialite3 = "socialite-3";

    // Badges (one-offs)
    public const string OffTheMark = "off-the-mark";
    public const string FirstBlood = "first-blood";
    public const string OnTheBoard = "on-the-board";
    public const string BeatTheCrowd = "beat-the-crowd";
    public const string EverPresent = "ever-present";
    public const string OnCall = "on-call";
    public const string Banked = "banked";
    public const string Founder = "founder";

    // Honours (placings)
    public const string Champion = "champion";
    public const string Podium = "podium";
    public const string RoundWinner = "round-winner";
    public const string MonthWinner = "month-winner";
    public const string StageWinner = "stage-winner";

    // Longevity
    public const string Veteran = "veteran";
}
