using System.Security.Claims;
using DevInsight.Application.DTOs;
using DevInsight.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class AnomalyController : ControllerBase
{
    private readonly ICommitRepository _commitRepo;
    private readonly IPullRequestRepository _prRepo;

    public AnomalyController(ICommitRepository commitRepo, IPullRequestRepository prRepo)
    {
        _commitRepo = commitRepo;
        _prRepo = prRepo;
    }

    /// <summary>Detect anomalies in commit/PR patterns for a repository.</summary>
    [HttpGet("{repositoryId:guid}")]
    public async Task<IActionResult> Detect(Guid repositoryId, [FromQuery] int lookbackDays = 90, CancellationToken ct = default)
    {
        var anomalies = new List<AnomalyDto>();
        var now = DateTime.UtcNow;
        var since = now.AddDays(-lookbackDays);

        var commits = (await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(c => c.AuthoredAt >= since).ToList();
        var prs = (await _prRepo.GetByRepositoryIdAsync(repositoryId, ct))
            .Where(p => p.OpenedAt >= since).ToList();

        if (commits.Count < 7) return Ok(new { message = "Not enough data for anomaly detection (need at least 7 commits).", anomalies });

        // 1. Daily commit frequency anomalies (z-score > 2)
        var dailyCounts = commits.GroupBy(c => c.AuthoredAt.Date).ToDictionary(g => g.Key, g => (double)g.Count());
        var allDays = Enumerable.Range(0, lookbackDays).Select(i => since.AddDays(i).Date).ToList();
        var dailySeries = allDays.Select(d => dailyCounts.GetValueOrDefault(d, 0)).ToList();
        var (mean, stdDev) = Stats(dailySeries);

        if (stdDev > 0)
        {
            foreach (var day in allDays)
            {
                var count = dailyCounts.GetValueOrDefault(day, 0);
                var zScore = (count - mean) / stdDev;
                if (zScore > 2.0)
                    anomalies.Add(new AnomalyDto("CommitSpike", $"Unusually high commit activity on {day:yyyy-MM-dd}: {count} commits (avg {mean:F1})", "Warning", day, new { day, count, mean = Math.Round(mean, 1), zScore = Math.Round(zScore, 1) }));
                else if (count == 0 && day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday && mean > 1)
                    anomalies.Add(new AnomalyDto("CommitGap", $"No commits on weekday {day:yyyy-MM-dd} (avg {mean:F1}/day)", "Info", day, new { day, mean = Math.Round(mean, 1) }));
            }
        }

        // 2. Large commit anomalies (changes > mean + 2*stdDev)
        var commitSizes = commits.Select(c => (double)(c.Additions + c.Deletions)).ToList();
        var (sizeMean, sizeStd) = Stats(commitSizes);
        if (sizeStd > 0)
        {
            foreach (var c in commits.Where(c => c.Additions + c.Deletions > sizeMean + 2 * sizeStd))
                anomalies.Add(new AnomalyDto("LargeCommit", $"Commit {c.Sha[..7]} has {c.Additions + c.Deletions} lines changed (avg {sizeMean:F0})", "Warning", c.AuthoredAt, new { sha = c.Sha[..7], lines = c.Additions + c.Deletions, avg = Math.Round(sizeMean) }));
        }

        // 3. Developer activity drop-off
        var recentWindow = now.AddDays(-7);
        var prevWindow = now.AddDays(-30);
        var recentDevs = commits.Where(c => c.AuthoredAt >= recentWindow).Select(c => c.AuthorEmail).Distinct().ToHashSet();
        var prevDevs = commits.Where(c => c.AuthoredAt >= prevWindow && c.AuthoredAt < recentWindow).Select(c => c.AuthorEmail).Distinct().ToList();
        var droppedOff = prevDevs.Where(d => !recentDevs.Contains(d)).ToList();
        if (droppedOff.Any())
            anomalies.Add(new AnomalyDto("DeveloperDropoff", $"{droppedOff.Count} developer(s) stopped committing in the last 7 days", "Warning", now, new { developers = droppedOff }));

        // 4. PR merge time anomalies
        var mergedPrs = prs.Where(p => p.MergedAt.HasValue).ToList();
        if (mergedPrs.Count >= 5)
        {
            var mergeTimes = mergedPrs.Select(p => (p.MergedAt!.Value - p.OpenedAt).TotalHours).ToList();
            var (mtMean, mtStd) = Stats(mergeTimes);
            foreach (var pr in mergedPrs.Where(p => (p.MergedAt!.Value - p.OpenedAt).TotalHours > mtMean + 2 * mtStd))
            {
                var hours = (pr.MergedAt!.Value - pr.OpenedAt).TotalHours;
                anomalies.Add(new AnomalyDto("SlowMerge", $"PR #{pr.Number} took {hours:F1}h to merge (avg {mtMean:F1}h)", "Warning", pr.MergedAt.Value, new { prNumber = pr.Number, hours = Math.Round(hours, 1), avgHours = Math.Round(mtMean, 1) }));
            }
        }

        return Ok(new { total = anomalies.Count, anomalies = anomalies.OrderByDescending(a => a.DetectedAt) });
    }

    private static (double mean, double stdDev) Stats(List<double> values)
    {
        if (values.Count == 0) return (0, 0);
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return (mean, Math.Sqrt(variance));
    }
}
