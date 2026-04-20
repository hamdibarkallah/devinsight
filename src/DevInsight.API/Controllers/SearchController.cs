using DevInsight.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _search;
    public SearchController(ISearchService search) => _search = search;

    /// <summary>Full-text search across commits and issues (requires Elasticsearch).</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] string? index = null, [FromQuery] int max = 20, CancellationToken ct = default)
    {
        if (!_search.IsAvailable)
            return Ok(new { message = "Search is not available. Configure Elasticsearch:Url in appsettings.", results = Array.Empty<object>() });

        var results = await _search.SearchAsync(q, index, max, ct);
        return Ok(new { total = results.Count, results });
    }
}
