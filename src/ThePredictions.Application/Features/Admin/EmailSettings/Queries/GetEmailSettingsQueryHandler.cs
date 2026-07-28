using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.EmailSettings;
using DomainEmailSettings = ThePredictions.Domain.Models.EmailSettings;

namespace ThePredictions.Application.Features.Admin.EmailSettings.Queries;

public class GetEmailSettingsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetEmailSettingsQuery, EmailSettingsDto>
{
    public async Task<EmailSettingsDto> Handle(GetEmailSettingsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1
                es.[EmailsEnabled]
            FROM
                [EmailSettings] es
            ORDER BY
                es.[Id];";

        var settings = await dbConnection.QuerySingleOrDefaultAsync<EmailSettingsQueryResult>(sql, cancellationToken);

        // Fall back to the built-in default if no row has been seeded yet (emails on).
        if (settings is not null)
            return new EmailSettingsDto(settings.EmailsEnabled);

        var defaults = DomainEmailSettings.CreateDefault();
        return new EmailSettingsDto(defaults.EmailsEnabled);
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record EmailSettingsQueryResult(
        bool EmailsEnabled);
}
