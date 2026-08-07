using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Account.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Badges;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Account.Commands;

/// <summary>
/// Saving the bank details a league administrator pays winnings into. Nothing reaches the database
/// in the clear - every field goes through encryption first - and putting an account number on the
/// account earns the "Banked" badge.
/// </summary>
public class SetPayoutDetailsCommandHandlerTests
{
    private const string UserId = "user-1";

    private static readonly DateTime NowUtc = new(2026, 7, 6, 10, 0, 0, DateTimeKind.Utc);

    private readonly IUserPayoutDetailsRepository _repository = Substitute.For<IUserPayoutDetailsRepository>();
    private readonly IFieldEncryptionService _encryption = Substitute.For<IFieldEncryptionService>();
    private readonly IBadgeAwardService _badgeAwardService = Substitute.For<IBadgeAwardService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private readonly SetPayoutDetailsCommandHandler _handler;

    public SetPayoutDetailsCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _encryption.Encrypt(Arg.Any<string?>()).Returns(call => call.Arg<string?>() is null ? null : $"enc({call.Arg<string?>()})");
        _handler = new SetPayoutDetailsCommandHandler(_repository, _encryption, _badgeAwardService, _dateTimeProvider);
    }

    private Task HandleAsync(string? accountName = "A Anderson", string? sortCode = "12-34-56", string? accountNumber = "12345678", string userId = UserId) =>
        _handler.Handle(new SetPayoutDetailsCommand(userId, accountName, sortCode, accountNumber), CancellationToken.None);

    private UserPayoutDetails CapturedDetails() =>
        (UserPayoutDetails)_repository.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IUserPayoutDetailsRepository.UpsertAsync))
            .GetArguments()[0]!;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRefuseAnEmptyUser(string? userId)
    {
        var act = () => HandleAsync(userId: userId!);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_ShouldNeverStoreBankDetailsInTheClear()
    {
        await HandleAsync();

        var stored = CapturedDetails();
        stored.AccountName.Should().Be("enc(A Anderson)");
        stored.SortCode.Should().Be("enc(12-34-56)");
        stored.AccountNumber.Should().Be("enc(12345678)");
    }

    [Fact]
    public async Task Handle_ShouldTrimStrayWhitespaceBeforeEncrypting()
    {
        await HandleAsync(accountName: "  A Anderson  ");

        _encryption.Received(1).Encrypt("A Anderson");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldTreatABlankFieldAsNotSupplied(string? accountName)
    {
        // A blank box means "I have not given you this", not "store an empty string".
        await HandleAsync(accountName: accountName);

        _encryption.Received().Encrypt(null);
        CapturedDetails().AccountName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCreateTheRecord_WhenNoneExistsYet()
    {
        await HandleAsync();

        var stored = CapturedDetails();
        stored.UserId.Should().Be(UserId);
        stored.CreatedAtUtc.Should().Be(NowUtc);
        stored.UpdatedAtUtc.Should().Be(NowUtc);
    }

    [Fact]
    public async Task Handle_ShouldUpdateTheExistingRecordRatherThanReplacingIt()
    {
        // The original creation date has to survive, so the existing row is edited in place.
        var createdAtUtc = NowUtc.AddYears(-1);
        var existing = new UserPayoutDetails(UserId, "enc(old)", "enc(old)", "enc(old)", createdAtUtc, createdAtUtc);
        _repository.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(existing);

        await HandleAsync();

        var stored = CapturedDetails();
        stored.Should().BeSameAs(existing);
        stored.CreatedAtUtc.Should().Be(createdAtUtc);
        stored.UpdatedAtUtc.Should().Be(NowUtc);
        stored.AccountNumber.Should().Be("enc(12345678)");
    }

    [Fact]
    public async Task Handle_ShouldAwardTheBankedBadge_WhenAnAccountNumberIsGiven()
    {
        await HandleAsync();

        await _badgeAwardService.Received(1).AwardAsync(UserId, BadgeKeys.Banked, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldNotAwardTheBankedBadge_WhenNoAccountNumberIsGiven(string? accountNumber)
    {
        await HandleAsync(accountNumber: accountNumber);

        await _badgeAwardService.DidNotReceiveWithAnyArgs().AwardAsync(default!, default!, CancellationToken.None);
    }
}
