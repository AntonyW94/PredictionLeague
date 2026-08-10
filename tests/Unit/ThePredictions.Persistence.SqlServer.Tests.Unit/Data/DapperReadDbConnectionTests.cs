using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Common.Exceptions;
using ThePredictions.Application.Configuration;
using ThePredictions.Application.Data;
using ThePredictions.Persistence.SqlServer.Data;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Unit.Data;

public class DapperReadDbConnectionTests
{
    private readonly IDbConnectionFactory _connectionFactory = Substitute.For<IDbConnectionFactory>();
    private readonly DapperReadDbConnection _readDbConnection;

    public DapperReadDbConnectionTests()
    {
        _readDbConnection = new DapperReadDbConnection(
            _connectionFactory,
            new PassThroughRetryPolicy(),
            Options.Create(new TimeoutSettings()),
            Options.Create(new QueryMonitoringSettings()),
            Substitute.For<ILogger<DapperReadDbConnection>>());
    }

    [Fact]
    public async Task QueryAsync_ShouldThrowReadQueryFailed_WhenTheReadThrowsInvalidOperation()
    {
        // Dapper reports a result set that does not match the requested result record as an
        // InvalidOperationException, which the API middleware would otherwise report as a 400 and a Warning.
        var materialisationFailure = new InvalidOperationException("A parameterless default constructor or one matching signature is required");
        _connectionFactory.CreateConnection().Throws(materialisationFailure);

        var act = () => _readDbConnection.QueryAsync<string>("SELECT [Name] FROM [Leagues]", CancellationToken.None);

        (await act.Should().ThrowAsync<ReadQueryFailedException>())
            .WithInnerException<InvalidOperationException>()
            .Which.Message.Should().Be(materialisationFailure.Message);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_ShouldThrowReadQueryFailed_WhenTheReadThrowsInvalidOperation()
    {
        _connectionFactory.CreateConnection().Throws(new InvalidOperationException("Sequence contains more than one element"));

        var act = () => _readDbConnection.QuerySingleOrDefaultAsync<string>("SELECT [Name] FROM [Leagues]", CancellationToken.None);

        await act.Should().ThrowAsync<ReadQueryFailedException>();
    }

    [Fact]
    public async Task QueryAsync_ShouldNotWrap_WhenTheReadThrowsAnyOtherException()
    {
        _connectionFactory.CreateConnection().Throws(new TimeoutException("Timeout expired"));

        var act = () => _readDbConnection.QueryAsync<string>("SELECT [Name] FROM [Leagues]", CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task QueryAsync_ShouldNotWrap_WhenTheRequestIsCancelled()
    {
        _connectionFactory.CreateConnection().Throws(new OperationCanceledException());

        var act = () => _readDbConnection.QueryAsync<string>("SELECT [Name] FROM [Leagues]", CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class PassThroughRetryPolicy : ISqlRetryPolicy
    {
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
        {
            return operation(cancellationToken);
        }
    }
}
