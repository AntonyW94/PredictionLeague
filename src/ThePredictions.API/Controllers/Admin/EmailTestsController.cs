using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThePredictions.Application.Features.Admin.EmailTests.Commands;
using ThePredictions.Application.Features.Admin.EmailTests.Queries;
using ThePredictions.Contracts.Admin.EmailTests;
using ThePredictions.Domain.Common.Enumerations;
using Swashbuckle.AspNetCore.Annotations;

namespace ThePredictions.API.Controllers.Admin;

[Authorize(Roles = nameof(ApplicationUserRole.Administrator))]
[ApiController]
[Route("api/admin/[controller]")]
[SwaggerTag("Admin: Email Tests - Trigger Brevo templates against your own inbox (Admin only)")]
public class EmailTestsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("templates")]
    [SwaggerOperation(
        Summary = "List Brevo templates",
        Description = "Returns every Brevo transactional template (active and inactive) with the merge-tag parameters discovered in each template's HTML.")]
    [SwaggerResponse(200, "Templates retrieved successfully", typeof(IReadOnlyList<EmailTestTemplateDto>))]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<IReadOnlyList<EmailTestTemplateDto>>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetEmailTestTemplatesQuery(), cancellationToken));
    }

    [HttpGet("templates/{templateId:long}/defaults")]
    [SwaggerOperation(
        Summary = "Get smart defaults for a template",
        Description = "Returns pre-filled default values for the template's parameters, seeded from the selected data-picker user.")]
    [SwaggerResponse(200, "Defaults retrieved successfully", typeof(EmailTestDefaultsDto))]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<EmailTestDefaultsDto>> GetDefaultsAsync(
        [SwaggerParameter("Brevo template identifier")] long templateId,
        [FromQuery, SwaggerParameter("User whose details seed the defaults")] string dataUserId,
        CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetEmailTestDefaultsQuery(templateId, dataUserId), cancellationToken));
    }

    [HttpPost("send")]
    [SwaggerOperation(
        Summary = "Send a test email",
        Description = "Sends the chosen template with the supplied parameters to the calling admin's own email address (never the data-picker user).")]
    [SwaggerResponse(200, "Send attempted - inspect result for success/failure", typeof(SendTestEmailResultDto))]
    [SwaggerResponse(403, "Not authorised - admin role required")]
    public async Task<ActionResult<SendTestEmailResultDto>> SendAsync(
        [FromBody, SwaggerParameter("Template and parameter values to send", Required = true)] SendTestEmailRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendTestEmailCommand(request.TemplateId, request.Parameters, CurrentUserId);
        return Ok(await mediator.Send(command, cancellationToken));
    }
}
