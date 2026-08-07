using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.RunningCosts.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.RunningCosts.Commands;

/// <summary>
/// The record of what it costs to run the site. These feed the season price recommendation, so the
/// figures have to survive an edit intact.
/// </summary>
public class RunningCostCommandHandlerTests
{
    private const int RunningCostId = 5;

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime StartDateUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IRunningCostRepository _repository = Substitute.For<IRunningCostRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly CreateRunningCostCommandHandler _create;
    private readonly UpdateRunningCostCommandHandler _update;
    private readonly DeleteRunningCostCommandHandler _delete;

    public RunningCostCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _create = new CreateRunningCostCommandHandler(_repository, _dateTimeProvider);
        _update = new UpdateRunningCostCommandHandler(_repository);
        _delete = new DeleteRunningCostCommandHandler(_repository);
    }

    private RunningCost GivenExisting() =>
        GivenExisting(new RunningCost(id: RunningCostId, name: "Hosting", amount: 10m,
            frequency: CostFrequency.Monthly, startDateUtc: StartDateUtc, endDateUtc: null,
            notes: null, createdAtUtc: NowUtc.AddMonths(-6)));

    private RunningCost GivenExisting(RunningCost cost)
    {
        _repository.GetByIdAsync(RunningCostId, Arg.Any<CancellationToken>()).Returns(cost);
        return cost;
    }

    private Task CreateAsync(string name = "Hosting", decimal amount = 10m, string? notes = null) =>
        _create.Handle(new CreateRunningCostCommand(name, amount, CostFrequency.Monthly, StartDateUtc, null, notes), CancellationToken.None);

    private Task UpdateAsync(string name = "Domain renewal", decimal amount = 25m) =>
        _update.Handle(new UpdateRunningCostCommand(RunningCostId, name, amount, CostFrequency.Annual,
            StartDateUtc, StartDateUtc.AddYears(1), "Renewed"), CancellationToken.None);

    private RunningCost CapturedNewCost() =>
        (RunningCost)_repository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IRunningCostRepository.AddAsync))
            .GetArguments()[0]!;

    [Fact]
    public async Task Create_ShouldRecordTheCost()
    {
        await CreateAsync(name: "Hosting", amount: 10m);

        var stored = CapturedNewCost();
        stored.Name.Should().Be("Hosting");
        stored.Amount.Should().Be(10m);
        stored.Frequency.Should().Be(CostFrequency.Monthly);
        stored.StartDateUtc.Should().Be(StartDateUtc);
        stored.CreatedAtUtc.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Create_ShouldTidyUpStrayWhitespace()
    {
        await CreateAsync(name: "  Hosting  ", notes: "  Paid yearly  ");

        var stored = CapturedNewCost();
        stored.Name.Should().Be("Hosting");
        stored.Notes.Should().Be("Paid yearly");
    }

    [Fact]
    public async Task Update_ShouldThrow_WhenTheCostDoesNotExist()
    {
        var act = () => UpdateAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Update_ShouldApplyTheEditedFiguresInPlace()
    {
        // The original creation date has to survive, so the existing record is edited rather than
        // replaced.
        var existing = GivenExisting();
        var createdAtUtc = existing.CreatedAtUtc;

        await UpdateAsync(name: "Domain renewal", amount: 25m);

        existing.Name.Should().Be("Domain renewal");
        existing.Amount.Should().Be(25m);
        existing.Frequency.Should().Be(CostFrequency.Annual);
        existing.EndDateUtc.Should().Be(StartDateUtc.AddYears(1));
        existing.Notes.Should().Be("Renewed");
        existing.CreatedAtUtc.Should().Be(createdAtUtc);
        await _repository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_ShouldRemoveTheCost()
    {
        await _delete.Handle(new DeleteRunningCostCommand(RunningCostId), CancellationToken.None);

        await _repository.Received(1).DeleteAsync(RunningCostId, Arg.Any<CancellationToken>());
    }
}
