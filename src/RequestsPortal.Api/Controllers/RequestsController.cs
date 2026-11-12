using Microsoft.AspNetCore.Mvc;
using RequestsPortal.Api.Contracts;
using RequestsPortal.Api.Mappings;
using RequestsPortal.Core.Enums;
using RequestsPortal.Core.Persistence;

namespace RequestsPortal.Api.Controllers;

[ApiController]
[Route("api/requests")]
public sealed class RequestsController : ControllerBase
{
    private readonly RequestRepository repository;

    public RequestsController(RequestRepository repository)
    {
        this.repository = repository;
    }

    [HttpPost]
    [ProducesResponseType<RequestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<RequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDto>> Create(
        [FromBody] CreateRequestBody body,
        CancellationToken cancellationToken)
    {
        var result = await repository.CreateAsync(body.ToCommand(), cancellationToken);
        var response = result.Request.ToDto();

        if (result.Created)
        {
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RequestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var request = await repository.GetAsync(id, cancellationToken);

        if (request is null)
        {
            return NotFound();
        }

        return Ok(request.ToDto());
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequestDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<RequestDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        RequestStatus? filter = null;

        if (Request.Query.ContainsKey("status"))
        {
            if (Request.Query["status"].Count != 1
                || !Enum.GetNames<RequestStatus>().Contains(status ?? string.Empty, StringComparer.Ordinal))
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["status"] = ["Usa un único estado: Pending, Processing, Completed o Failed."]
                }));
            }

            filter = Enum.Parse<RequestStatus>(status!);
        }

        var requests = await repository.ListAsync(filter, cancellationToken);
        return Ok(requests.Select(request => request.ToDto()).ToArray());
    }
}
