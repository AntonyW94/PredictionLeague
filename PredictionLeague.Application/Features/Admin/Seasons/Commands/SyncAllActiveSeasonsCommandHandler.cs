using MediatR;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Seasons.Commands;

public class SyncAllActiveSeasonsCommandHandler : IRequestHandler<SyncAllActiveSeasonsCommand>
{
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMediator _mediator;

    public SyncAllActiveSeasonsCommandHandler(ISeasonRepository seasonRepository, IMediator mediator)
    {
        _seasonRepository = seasonRepository;
        _mediator = mediator;
    }

    public async Task Handle(SyncAllActiveSeasonsCommand request, CancellationToken cancellationToken)
    {
        var activeSeasons = await _seasonRepository.GetActiveSeasonsAsync(cancellationToken);

        foreach (var season in activeSeasons)
        {
            await _mediator.Send(new SyncSeasonWithApiCommand(season.Id), cancellationToken);
        }
    }
}