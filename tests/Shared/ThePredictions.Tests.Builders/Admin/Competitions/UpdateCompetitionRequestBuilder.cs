using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Tests.Builders.Admin.Competitions;

public class UpdateCompetitionRequestBuilder
{
    private string _code = "PREM";
    private string _name = "Premier League";
    private int _type;
    private string? _logoUrl = "https://example.com/logo.png";
    private string? _description = "The English top flight.";
    private int? _apiLeagueId = 39;

    public UpdateCompetitionRequestBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }

    public UpdateCompetitionRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UpdateCompetitionRequestBuilder WithType(int type)
    {
        _type = type;
        return this;
    }

    public UpdateCompetitionRequestBuilder WithLogoUrl(string? logoUrl)
    {
        _logoUrl = logoUrl;
        return this;
    }

    public UpdateCompetitionRequestBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public UpdateCompetitionRequestBuilder WithApiLeagueId(int? apiLeagueId)
    {
        _apiLeagueId = apiLeagueId;
        return this;
    }

    public UpdateCompetitionRequest Build() => new()
    {
        Code = _code,
        Name = _name,
        Type = _type,
        LogoUrl = _logoUrl,
        Description = _description,
        ApiLeagueId = _apiLeagueId
    };
}
