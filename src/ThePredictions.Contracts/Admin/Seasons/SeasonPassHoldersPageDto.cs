using ThePredictions.Contracts.Common;

namespace ThePredictions.Contracts.Admin.Seasons;

/// <summary>
/// One page of Season Pass holders, plus the season it belongs to and the money collected
/// across the whole matching set (so the header total does not change as you page through).
/// </summary>
public record SeasonPassHoldersPageDto(
    string SeasonName,
    decimal TotalCollected,
    PagedResult<SeasonPassHolderDto> Holders
);
