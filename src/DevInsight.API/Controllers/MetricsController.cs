using DevInsight.Application.Features.Metrics.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevInsight.API.Controllers;
[ApiController, Route("api/[controller]"), Authorize]
public class MetricsController : ControllerBase
{
    private readonly IMediator _mediator;
    public MetricsController(IMediator mediator) => _mediator = mediator;
    [HttpGet("repository/{repositoryId:guid}")] public async Task<IActionResult> GetMetrics(Guid repositoryId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct) => Ok(await _mediator.Send(new GetRepositoryMetricsQuery(repositoryId, from, to), ct));
}
