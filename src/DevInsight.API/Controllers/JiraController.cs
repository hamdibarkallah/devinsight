using System.Security.Claims;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class JiraController : ControllerBase
{
    private readonly IJiraService _jiraService;
    private readonly IIssueRepository _issueRepo;
    private readonly IUnitOfWork _unitOfWork;

    public JiraController(IJiraService jiraService, IIssueRepository issueRepo, IUnitOfWork unitOfWork)
    {
        _jiraService = jiraService;
        _issueRepo = issueRepo;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Sync issues from a Jira project.</summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncIssues([FromBody] JiraSyncRequest request, CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var remoteIssues = await _jiraService.GetIssuesAsync(request.BaseUrl, request.Email, request.ApiToken, request.ProjectKey, request.Since, ct);

        var existing = await _issueRepo.GetByOrganizationIdAsync(orgId, ct);
        var existingIds = existing.Select(i => i.ExternalId).ToHashSet();

        int added = 0;
        foreach (var issue in remoteIssues)
        {
            if (existingIds.Contains(issue.ExternalId)) continue;
            issue.OrganizationId = orgId;
            await _issueRepo.AddAsync(issue, ct);
            added++;
        }
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = $"Synced {remoteIssues.Count} issues from Jira project {request.ProjectKey}. {added} new.", total = remoteIssues.Count, newlyAdded = added });
    }

    /// <summary>List synced issues for the current org.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var issues = await _issueRepo.GetByOrganizationIdAsync(orgId, ct);

        if (from.HasValue) issues = issues.Where(i => i.CreatedDate >= from.Value).ToList();
        if (to.HasValue) issues = issues.Where(i => i.CreatedDate <= to.Value).ToList();

        return Ok(issues.Select(i => new
        {
            i.Key, i.Summary, Status = i.Status.ToString(), i.Assignee,
            i.IssueType, i.Priority, i.CreatedDate, i.ResolvedDate, i.CycleTimeHours
        }));
    }

    /// <summary>Issue analytics — cycle time, throughput, status distribution.</summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> Analytics([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var issues = (await _issueRepo.GetByOrganizationIdAsync(orgId, ct)).ToList();

        if (from.HasValue) issues = issues.Where(i => i.CreatedDate >= from.Value).ToList();
        if (to.HasValue) issues = issues.Where(i => i.CreatedDate <= to.Value).ToList();

        var resolved = issues.Where(i => i.CycleTimeHours.HasValue).ToList();

        return Ok(new
        {
            totalIssues = issues.Count,
            statusDistribution = issues.GroupBy(i => i.Status.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            avgCycleTimeHours = resolved.Any() ? Math.Round(resolved.Average(i => i.CycleTimeHours!.Value), 1) : 0,
            medianCycleTimeHours = resolved.Any() ? Math.Round(Median(resolved.Select(i => i.CycleTimeHours!.Value).ToList()), 1) : 0,
            throughputPerWeek = issues.Count > 0 ? Math.Round(resolved.Count / Math.Max(1, (issues.Max(i => i.CreatedDate) - issues.Min(i => i.CreatedDate)).TotalDays / 7.0), 1) : 0,
            byAssignee = issues.GroupBy(i => i.Assignee ?? "Unassigned").Select(g => new
            {
                assignee = g.Key, total = g.Count(),
                resolved = g.Count(i => i.ResolvedDate.HasValue),
                avgCycleTime = g.Where(i => i.CycleTimeHours.HasValue).Any()
                    ? Math.Round(g.Where(i => i.CycleTimeHours.HasValue).Average(i => i.CycleTimeHours!.Value), 1) : 0
            })
        });
    }

    private static double Median(List<double> values)
    {
        values.Sort();
        int mid = values.Count / 2;
        return values.Count % 2 == 0 ? (values[mid - 1] + values[mid]) / 2.0 : values[mid];
    }
}

public record JiraSyncRequest(string BaseUrl, string Email, string ApiToken, string ProjectKey, DateTime? Since = null);
