using System.Security.Claims;
using DevInsight.Application.DTOs;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
using DevInsight.Domain.Interfaces;
using DevInsight.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly ICommitRepository _commitRepo;
    private readonly IPullRequestRepository _prRepo;
    private readonly IRepository<Repository> _repoRepo;
    private readonly ICacheService _cache;

    public AnalyticsController(ICommitRepository commitRepo, IPullRequestRepository prRepo, IRepository<Repository> repoRepo, ICacheService cache)
    {
        _commitRepo = commitRepo;
        _prRepo = prRepo;
        _repoRepo = repoRepo;
        _cache = cache;
    }

    /// <summary>Per-developer activity stats for a repository within a date range.</summary>
    [HttpGet("developers/{repositoryId:guid}")]
    public async Task<IActionResult> GetDeveloperStats(Guid repositoryId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var toEnd = to.Date.AddDays(1).AddTicks(-1);
        var commits = await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct);
        var prs = await _prRepo.GetByRepositoryIdAsync(repositoryId, ct);

        var filtered = commits.Where(c => c.AuthoredAt >= from && c.AuthoredAt <= toEnd).ToList();
        var filteredPrs = prs.Where(p => p.OpenedAt >= from && p.OpenedAt <= toEnd).ToList();

        var devStats = filtered
            .GroupBy(c => new { c.AuthorName, c.AuthorEmail })
            .Select(g =>
            {
                var authorPrs = filteredPrs.Where(p => p.AuthorName == g.Key.AuthorName).ToList();
                var mergedPrs = authorPrs.Where(p => p.State == PullRequestState.Merged && p.MergedAt.HasValue).ToList();
                var avgLeadTime = mergedPrs.Any()
                    ? mergedPrs.Average(p => (p.MergedAt!.Value - p.OpenedAt).TotalHours)
                    : 0;

                return new DeveloperStatsDto(
                    g.Key.AuthorName, g.Key.AuthorEmail,
                    g.Count(),
                    g.Sum(c => c.Additions), g.Sum(c => c.Deletions),
                    g.Sum(c => c.Additions - c.Deletions),
                    authorPrs.Count, mergedPrs.Count, Math.Round(avgLeadTime, 1)
                );
            })
            .OrderByDescending(d => d.TotalCommits)
            .ToList();

        return Ok(devStats);
    }

    /// <summary>Team velocity — aggregated metrics per time period.</summary>
    [HttpGet("velocity/{repositoryId:guid}")]
    public async Task<IActionResult> GetTeamVelocity(Guid repositoryId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var toEnd = to.Date.AddDays(1).AddTicks(-1);
        var commits = (await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(c => c.AuthoredAt >= from && c.AuthoredAt <= toEnd).ToList();
        var prs = (await _prRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(p => p.MergedAt.HasValue && p.MergedAt >= from && p.MergedAt <= toEnd).ToList();

        var activeDevelopers = commits.Select(c => c.AuthorEmail).Distinct().Count();

        var result = new TeamVelocityDto(
            from, to,
            commits.Count, prs.Count,
            commits.Sum(c => c.Additions), commits.Sum(c => c.Deletions),
            activeDevelopers,
            activeDevelopers > 0 ? Math.Round((double)commits.Count / activeDevelopers, 1) : 0
        );
        return Ok(result);
    }

    /// <summary>Daily trends for commits and PRs over a date range.</summary>
    [HttpGet("trends/{repositoryId:guid}")]
    public async Task<IActionResult> GetTrends(Guid repositoryId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var toEnd = to.Date.AddDays(1).AddTicks(-1);
        var commits = (await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(c => c.AuthoredAt >= from && c.AuthoredAt <= toEnd).ToList();
        var prs = (await _prRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(p => p.MergedAt.HasValue && p.MergedAt >= from && p.MergedAt <= toEnd).ToList();

        var days = Enumerable.Range(0, (int)(to - from).TotalDays + 1)
            .Select(offset => from.AddDays(offset).Date)
            .ToList();

        var trends = days.Select(day =>
        {
            var dayCommits = commits.Where(c => c.AuthoredAt.Date == day).ToList();
            var dayPrs = prs.Where(p => p.MergedAt!.Value.Date == day).ToList();
            return new TrendDataPointDto(day, dayCommits.Count, dayPrs.Count,
                dayCommits.Sum(c => c.Additions), dayCommits.Sum(c => c.Deletions));
        }).ToList();

        return Ok(trends);
    }

    /// <summary>PR cycle times — lead time and merge time for each PR.</summary>
    [HttpGet("cycle-time/{repositoryId:guid}")]
    public async Task<IActionResult> GetCycleTimes(Guid repositoryId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var toEnd = to.Date.AddDays(1).AddTicks(-1);
        var prs = (await _prRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(p => p.OpenedAt >= from && p.OpenedAt <= toEnd)
            .Select(p => new PrCycleTimeDto(
                p.Number, p.Title, p.AuthorName,
                p.ClosedAt.HasValue ? (p.ClosedAt.Value - p.OpenedAt).TotalHours : (DateTime.UtcNow - p.OpenedAt).TotalHours,
                p.MergedAt.HasValue ? (p.MergedAt.Value - p.OpenedAt).TotalHours : null,
                p.State.ToString()
            ))
            .OrderByDescending(p => p.LeadTimeHours)
            .ToList();

        return Ok(new
        {
            pullRequests = prs,
            avgLeadTimeHours = prs.Any() ? Math.Round(prs.Average(p => p.LeadTimeHours), 1) : 0,
            avgTimeToMergeHours = prs.Where(p => p.TimeToMergeHours.HasValue).Any()
                ? Math.Round(prs.Where(p => p.TimeToMergeHours.HasValue).Average(p => p.TimeToMergeHours!.Value), 1) : 0
        });
    }

    /// <summary>Bottleneck detection — stale PRs, low-activity devs, large PRs.</summary>
    [HttpGet("bottlenecks/{repositoryId:guid}")]
    public async Task<IActionResult> GetBottlenecks(Guid repositoryId, CancellationToken ct)
    {
        var prs = await _prRepo.GetByRepositoryIdAsync(repositoryId, ct);
        var commits = await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct);
        var bottlenecks = new List<BottleneckDto>();

        // Stale open PRs (open > 7 days)
        var stalePrs = prs.Where(p => p.State == PullRequestState.Open && (DateTime.UtcNow - p.OpenedAt).TotalDays > 7).ToList();
        if (stalePrs.Any())
            bottlenecks.Add(new BottleneckDto("StalePullRequests",
                $"{stalePrs.Count} PR(s) open for more than 7 days", "Warning",
                stalePrs.Select(p => new { p.Number, p.Title, p.AuthorName, DaysOpen = Math.Round((DateTime.UtcNow - p.OpenedAt).TotalDays, 1) })));

        // Large PRs (>500 lines changed)
        var largePrs = prs.Where(p => p.Additions + p.Deletions > 500).ToList();
        if (largePrs.Any())
            bottlenecks.Add(new BottleneckDto("LargePullRequests",
                $"{largePrs.Count} PR(s) with more than 500 lines changed", "Info",
                largePrs.Select(p => new { p.Number, p.Title, TotalChanges = p.Additions + p.Deletions })));

        // Inactive devs (committed in last 30 days but not last 7)
        var recentAuthors = commits.Where(c => c.AuthoredAt >= DateTime.UtcNow.AddDays(-7)).Select(c => c.AuthorEmail).ToHashSet();
        var monthAuthors = commits.Where(c => c.AuthoredAt >= DateTime.UtcNow.AddDays(-30)).Select(c => c.AuthorEmail).Distinct().ToList();
        var inactive = monthAuthors.Where(a => !recentAuthors.Contains(a)).ToList();
        if (inactive.Any())
            bottlenecks.Add(new BottleneckDto("InactiveDevelopers",
                $"{inactive.Count} dev(s) active in last 30 days but not last 7", "Warning",
                inactive));

        return Ok(bottlenecks);
    }
}
