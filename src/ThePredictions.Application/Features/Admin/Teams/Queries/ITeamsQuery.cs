namespace ThePredictions.Application.Features.Admin.Teams.Queries;

/// <summary>Reads every team, in no order.</summary>
public interface ITeamsQuery
{
    Task<IReadOnlyList<TeamRow>> ExecuteAsync(CancellationToken cancellationToken);
}
