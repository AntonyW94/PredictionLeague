namespace ThePredictions.Application.Features.Admin.Competitions.Queries;

/// <summary>Reads every competition, in no order.</summary>
/// <remarks>
/// One read serves both the list and the single-competition screen. They had a statement each, differing only in a
/// <c>WHERE</c> clause and repeating the same eight columns and the same season count - and there are three
/// competitions in the database, so picking one out of the set costs nothing worth a second statement.
/// </remarks>
public interface ICompetitionsQuery
{
    Task<IReadOnlyList<CompetitionRow>> ExecuteAsync(CancellationToken cancellationToken);
}
