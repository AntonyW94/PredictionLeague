namespace ThePredictions.Application.Features.Dashboard.Queries;

/// <summary>
/// What the dashboard needs to know about a league in order to place its tile.
/// </summary>
/// <remarks>
/// Implemented by both dashboard tiles' row types so <see cref="LeagueTileOrder"/> can order either. The
/// alternative was the same four-clause ordering written out twice, which is how it got into this state: the
/// leaderboards tile had it in SQL and in LINQ at once, and the My Leagues tile had its own third copy.
/// </remarks>
public interface ILeagueTile
{
    bool HasRoundInProgress { get; }

    /// <summary>Never null - <c>Seasons.StartDateUtc</c> is a required column.</summary>
    DateTime SeasonStartDateUtc { get; }

    decimal Price { get; }

    string LeagueName { get; }
}
