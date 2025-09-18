namespace PredictionLeague.Contracts.Admin.Teams;

public record TeamDto(
    int Id,
    string Name,
    string ShortName,
    string LogoUrl,
    string Abbreviation,
    int? ApiTeamId
);