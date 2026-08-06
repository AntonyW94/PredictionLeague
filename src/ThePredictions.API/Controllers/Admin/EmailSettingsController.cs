using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.EmailSettings.Commands;
using ThePredictions.Application.Features.Admin.EmailSettings.Queries;
using ThePredictions.Contracts.Admin.EmailSettings;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Email settings - Master switch for automated emails (Admin only)")]
[ExcludeFromCodeCoverage(Justification = "Controller action: forwards to MediatR and returns the result. The behaviour under test is the handler.")]
public class EmailSettingsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get the email master switch",
        Description = "Returns whether the app is currently sending automated, transactional emails.")]
    [SwaggerResponse(200, "Settings retrieved successfully", typeof(EmailSettingsDto))]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<EmailSettingsDto>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetEmailSettingsQuery(), cancellationToken));
    }

    [HttpPut]
    [SwaggerOperation(
        Summary = "Update the email master switch",
        Description = "Turns automated, transactional emails on or off. The admin email-test tool is unaffected.")]
    [SwaggerResponse(204, "Settings updated successfully")]
    [SwaggerResponse(401, "Not authenticated")]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<IActionResult> UpdateAsync(
        [FromBody, SwaggerParameter("The new master-switch value", Required = true)] UpdateEmailSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmailSettingsCommand(request.EmailsEnabled), cancellationToken);

        return NoContent();
    }
}
