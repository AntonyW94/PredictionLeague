using ThePredictions.Contracts.Admin.Competitions;

namespace ThePredictions.Tests.Builders.Admin.Competitions;

public class CreateCompetitionRequestBuilder
{
    private string _code = "PREM";
    private string _name = "Premier League";
    private int _type;
    private string? _logoUrl = "https://example.com/logo.png";
    private string? _description = "The English top flight.";
    private int? _apiLeagueId = 39;

    public CreateCompetitionRequestBuilder WithCode(string code)
    {
        _code = code;
        return this;
    }

    public CreateCompetitionRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CreateCompetitionRequestBuilder WithType(int type)
    {
        _type = type;
        return this;
    }

    public CreateCompetitionRequestBuilder WithLogoUrl(string? logoUrl)
    {
        _logoUrl = logoUrl;
        return this;
    }

    public CreateCompetitionRequestBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public CreateCompetitionRequestBuilder WithApiLeagueId(int? apiLeagueId)
    {
        _apiLeagueId = apiLeagueId;
        return this;
    }

    public CreateCompetitionRequest Build() => new()
    {
        Code = _code,
        Name = _name,
        Type = _type,
        LogoUrl = _logoUrl,
        Description = _description,
        ApiLeagueId = _apiLeagueId
    };
}
