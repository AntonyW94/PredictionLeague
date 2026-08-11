namespace ThePredictions.Application.Features.Admin.Teams.Queries;

/// <summary>Reads the teams that appear in a season's fixtures, in no order.</summary>
/// <remarks>
/// Which teams are "in" a season is not stored anywhere - it is worked out from the fixtures, and a knockout fixture
/// whose teams are not known yet contributes none. Two statements asked that question in two shapes, an
/// <c>INNER JOIN ... DISTINCT</c> on the administrator's screen and an <c>EXISTS</c> on the season-pass page, and this
/// is the one read behind both.
/// </remarks>
public interface ISeasonTeamsQuery
{
    Task<IReadOnlyList<TeamRow>> ExecuteAsync(int seasonId, CancellationToken cancellationToken);
}
