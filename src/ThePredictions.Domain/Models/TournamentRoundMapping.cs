using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Models;

public class TournamentRoundMapping
{
    public int Id { get; init; }
    public int SeasonId { get; private init; }
    public int RoundNumber { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Stages { get; private set; } = string.Empty;
    public int ExpectedMatchCount { get; private set; }

    [ExcludeFromCodeCoverage]
    private TournamentRoundMapping() { }

    public TournamentRoundMapping(int id, int seasonId, int roundNumber, string displayName, string stages, int expectedMatchCount)
    {
        Id = id;
        SeasonId = seasonId;
        RoundNumber = roundNumber;
        DisplayName = displayName;
        Stages = stages;
        ExpectedMatchCount = expectedMatchCount;
    }

    public static TournamentRoundMapping Create(int seasonId, int roundNumber, string displayName, string stages, int expectedMatchCount)
    {
        Guard.Against.NegativeOrZero(seasonId, parameterName: null, message: "Season ID must be greater than 0");
        Guard.Against.NegativeOrZero(roundNumber, parameterName: null, message: "Round Number must be greater than 0");
        Guard.Against.NullOrWhiteSpace(displayName, message: "Please enter a Display Name");
        Guard.Against.NullOrWhiteSpace(stages, message: "At least one stage must be selected");
        Guard.Against.NegativeOrZero(expectedMatchCount, parameterName: null, message: "Expected Match Count must be greater than 0");

        return new TournamentRoundMapping
        {
            SeasonId = seasonId,
            RoundNumber = roundNumber,
            DisplayName = displayName,
            Stages = stages,
            ExpectedMatchCount = expectedMatchCount
        };
    }

    public List<TournamentStage> GetStageList()
    {
        if (string.IsNullOrWhiteSpace(Stages))
            return [];

        return Stages
            .Split('|')
            .Where(s => Enum.TryParse<TournamentStage>(s, out _))
            .Select(s => Enum.Parse<TournamentStage>(s))
            .ToList();
    }

    public TournamentStage GetPrimaryStage()
    {
        var stages = GetStageList();

        if (!stages.Any())
            throw new BusinessRuleViolationException("Mapping has no valid stages.");

        return stages[0];
    }
}
