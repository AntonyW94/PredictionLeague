using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Competitions.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Competitions.Commands;

/// <summary>
/// Managing the list of competitions a season can be run against. Codes have to stay unique because
/// they are how a competition is looked up, and one that still has seasons cannot be removed.
/// </summary>
public class CompetitionCommandHandlerTests
{
    private const int CompetitionId = 3;

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly ICompetitionRepository _repository = Substitute.For<ICompetitionRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly CreateCompetitionCommandHandler _create;
    private readonly UpdateCompetitionCommandHandler _update;
    private readonly DeleteCompetitionCommandHandler _delete;

    public CompetitionCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _create = new CreateCompetitionCommandHandler(_repository, _currentUser, _dateTimeProvider);
        _update = new UpdateCompetitionCommandHandler(_repository, _currentUser);
        _delete = new DeleteCompetitionCommandHandler(_repository, _currentUser);

        _repository.CreateAsync(Arg.Any<Competition>(), Arg.Any<CancellationToken>())
            .Returns(call => Stored(call.Arg<Competition>()));
    }

    private static Competition Competition(int id = CompetitionId, string code = "UCL", string name = "Champions League") =>
        new(id: id, code: code, name: name, type: CompetitionType.Tournament, logoUrl: null,
            description: null, apiLeagueId: 2, createdAtUtc: NowUtc);

    private static Competition Stored(Competition source) =>
        new(id: CompetitionId, code: source.Code, name: source.Name, type: source.Type,
            logoUrl: source.LogoUrl, description: source.Description, apiLeagueId: source.ApiLeagueId,
            createdAtUtc: source.CreatedAtUtc);

    private Competition GivenExisting(int id = CompetitionId, string code = "UCL")
    {
        var competition = Competition(id, code);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(competition);
        return competition;
    }

    private void GivenCodeTakenBy(Competition competition) =>
        _repository.GetByCodeAsync(competition.Code, Arg.Any<CancellationToken>()).Returns(competition);

    private Task<Contracts.Admin.Competitions.CompetitionDto> CreateAsync(string code = "UCL") =>
        _create.Handle(new CreateCompetitionCommand(code, "Champions League", CompetitionType.Tournament, null, null, 2), CancellationToken.None);

    private Task UpdateAsync(int id = CompetitionId, string code = "UCL", string name = "Champions League") =>
        _update.Handle(new UpdateCompetitionCommand(id, code, name, CompetitionType.League, "logo.png", "Desc", 39), CancellationToken.None);

    private Task DeleteAsync(int id = CompetitionId) =>
        _delete.Handle(new DeleteCompetitionCommand(id), CancellationToken.None);

    [Fact]
    public async Task Create_ShouldRequireAnAdministrator()
    {
        await CreateAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Create_ShouldRefuseACodeThatIsAlreadyInUse()
    {
        GivenCodeTakenBy(Competition());

        var act = () => CreateAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*code 'UCL' already exists*");
        await _repository.DidNotReceiveWithAnyArgs().CreateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Create_ShouldSaveTheCompetitionAndReportItBack()
    {
        var result = await CreateAsync();

        await _repository.Received(1).CreateAsync(
            Arg.Is<Competition>(c => c.Code == "UCL" && c.CreatedAtUtc == NowUtc), Arg.Any<CancellationToken>());
        result.Id.Should().Be(CompetitionId);
        result.Code.Should().Be("UCL");
        result.Type.Should().Be((int)CompetitionType.Tournament);
    }

    [Fact]
    public async Task Create_ShouldReportANewCompetitionAsHavingNoSeasons()
    {
        // Nothing can have been scheduled against it yet, so the count is fixed rather than read.
        var result = await CreateAsync();

        result.SeasonCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_ShouldRequireAnAdministrator()
    {
        GivenExisting();

        await UpdateAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Update_ShouldThrow_WhenTheCompetitionDoesNotExist()
    {
        var act = () => UpdateAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Update_ShouldRefuseACodeAnotherCompetitionAlreadyUses()
    {
        GivenExisting();
        GivenCodeTakenBy(Competition(id: 99, code: "UCL"));

        var act = () => UpdateAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*code 'UCL' already exists*");
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Update_ShouldAllowACompetitionToKeepItsOwnCode()
    {
        // The uniqueness check must not trip over the row being edited.
        var existing = GivenExisting();
        GivenCodeTakenBy(existing);

        await UpdateAsync();

        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_ShouldApplyTheEditedDetails()
    {
        var existing = GivenExisting();

        await UpdateAsync(name: "Premier League");

        existing.Name.Should().Be("Premier League");
        existing.Type.Should().Be(CompetitionType.League);
        existing.LogoUrl.Should().Be("logo.png");
        existing.Description.Should().Be("Desc");
        existing.ApiLeagueId.Should().Be(39);
    }

    [Fact]
    public async Task Delete_ShouldRequireAnAdministrator()
    {
        GivenExisting();

        await DeleteAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Delete_ShouldThrow_WhenTheCompetitionDoesNotExist()
    {
        var act = () => DeleteAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Delete_ShouldRefuse_WhileSeasonsStillReferToIt()
    {
        // Removing it would orphan those seasons, so the whole delete is rejected.
        GivenExisting();
        _repository.HasSeasonsAsync(CompetitionId, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => DeleteAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*still has seasons*");
        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Delete_ShouldRemoveACompetitionNothingUses()
    {
        GivenExisting();

        await DeleteAsync();

        await _repository.Received(1).DeleteAsync(CompetitionId, Arg.Any<CancellationToken>());
    }
}
