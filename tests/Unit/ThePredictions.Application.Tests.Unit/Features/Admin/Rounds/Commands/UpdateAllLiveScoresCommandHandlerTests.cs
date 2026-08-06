using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// The every-minute score refresh. It fans out across active seasons, so a site running two
/// competitions at once must update both rather than just the first.
/// </summary>
public class UpdateAllLiveScoresCommandHandlerTests
{
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private UpdateAllLiveScoresCommandHandler BuildHandler() => new(_seasonRepository, _mediator);

    private static Season Season(int id) =>
        new(id: id, name: $"Season {id}", startDateUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            endDateUtc: new DateTime(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc), isActive: true,
            numberOfRounds: 38, competitionId: 1, passStandardPrice: null, passPremiumPrice: null);

    private void GivenActiveSeasons(params Season[] seasons) =>
        _seasonRepository.GetActiveSeasonsAsync(Arg.Any<CancellationToken>()).Returns(seasons.ToList());

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoSeasonIsActive()
    {
        GivenActiveSeasons();

        await BuildHandler().Handle(new UpdateAllLiveScoresCommand(), CancellationToken.None);

        await _mediator.DidNotReceiveWithAnyArgs().Send(Arg.Any<UpdateScoresForNextRoundCommand>(), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRefreshScoresForTheActiveSeason()
    {
        GivenActiveSeasons(Season(7));

        await BuildHandler().Handle(new UpdateAllLiveScoresCommand(), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateScoresForNextRoundCommand>(c => c.SeasonId == 7), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefreshEverySeasonRunningAtOnce()
    {
        GivenActiveSeasons(Season(7), Season(8));

        await BuildHandler().Handle(new UpdateAllLiveScoresCommand(), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<UpdateScoresForNextRoundCommand>(c => c.SeasonId == 7), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<UpdateScoresForNextRoundCommand>(c => c.SeasonId == 8), Arg.Any<CancellationToken>());
    }
}
