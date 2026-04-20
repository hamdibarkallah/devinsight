using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
using DevInsight.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]")]
public class WebhooksController : ControllerBase
{
    private readonly ICommitRepository _commitRepo;
    private readonly IPullRequestRepository _prRepo;
    private readonly IRepository<Repository> _repoRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(ICommitRepository commitRepo, IPullRequestRepository prRepo,
        IRepository<Repository> repoRepo, IUnitOfWork unitOfWork, IConfiguration config, ILogger<WebhooksController> logger)
    {
        _commitRepo = commitRepo; _prRepo = prRepo; _repoRepo = repoRepo;
        _unitOfWork = unitOfWork; _config = config; _logger = logger;
    }

    /// <summary>GitHub webhook endpoint. Set your webhook URL to: POST /api/webhooks/github</summary>
    [HttpPost("github")]
    public async Task<IActionResult> GitHubWebhook(CancellationToken ct)
    {
        // Read raw body for signature verification
        Request.EnableBuffering();
        var body = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        Request.Body.Position = 0;

        // Verify signature if webhook secret is configured
        var secret = _config["GitHub:WebhookSecret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (signature is null || !VerifySignature(body, signature, secret))
            {
                _logger.LogWarning("Invalid webhook signature");
                return Unauthorized(new { message = "Invalid signature" });
            }
        }

        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault();
        _logger.LogInformation("Received GitHub webhook: {Event}", eventType);

        return eventType switch
        {
            "push" => await HandlePush(body, ct),
            "pull_request" => await HandlePullRequest(body, ct),
            "ping" => Ok(new { message = "pong" }),
            _ => Ok(new { message = $"Event {eventType} ignored." })
        };
    }

    private async Task<IActionResult> HandlePush(string body, CancellationToken ct)
    {
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var repoFullName = root.GetProperty("repository").GetProperty("full_name").GetString()!;
        var repos = await _repoRepo.GetAllAsync(ct);
        var repo = repos.FirstOrDefault(r => r.FullName == repoFullName);
        if (repo is null) return Ok(new { message = $"Repo {repoFullName} not tracked." });

        var commits = root.GetProperty("commits");
        var existingShas = (await _commitRepo.GetByRepositoryIdAsync(repo.Id, ct)).Select(c => c.Sha).ToHashSet();

        int added = 0;
        foreach (var c in commits.EnumerateArray())
        {
            var sha = c.GetProperty("id").GetString()!;
            if (existingShas.Contains(sha)) continue;

            await _commitRepo.AddAsync(new Commit
            {
                Sha = sha,
                Message = c.GetProperty("message").GetString()?[..Math.Min(c.GetProperty("message").GetString()!.Length, 500)] ?? "",
                AuthorName = c.GetProperty("author").GetProperty("name").GetString() ?? "",
                AuthorEmail = c.GetProperty("author").GetProperty("email").GetString() ?? "",
                AuthoredAt = c.GetProperty("timestamp").GetDateTime(),
                RepositoryId = repo.Id
            }, ct);
            added++;
        }
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Webhook push: {Added} commits for {Repo}", added, repoFullName);
        return Ok(new { message = $"{added} commits added for {repoFullName}" });
    }

    private async Task<IActionResult> HandlePullRequest(string body, CancellationToken ct)
    {
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var action = root.GetProperty("action").GetString();
        var prNode = root.GetProperty("pull_request");
        var repoFullName = root.GetProperty("repository").GetProperty("full_name").GetString()!;

        var repos = await _repoRepo.GetAllAsync(ct);
        var repo = repos.FirstOrDefault(r => r.FullName == repoFullName);
        if (repo is null) return Ok(new { message = $"Repo {repoFullName} not tracked." });

        var externalId = prNode.GetProperty("id").GetInt64().ToString();
        var existingPrs = await _prRepo.GetByRepositoryIdAsync(repo.Id, ct);
        var existing = existingPrs.FirstOrDefault(p => p.ExternalId == externalId);

        if (existing is null)
        {
            await _prRepo.AddAsync(new PullRequest
            {
                ExternalId = externalId,
                Number = prNode.GetProperty("number").GetInt32(),
                Title = prNode.GetProperty("title").GetString()?[..Math.Min(prNode.GetProperty("title").GetString()!.Length, 300)] ?? "",
                State = MapPrState(prNode),
                AuthorName = prNode.GetProperty("user").GetProperty("login").GetString() ?? "",
                OpenedAt = prNode.GetProperty("created_at").GetDateTime(),
                ClosedAt = prNode.TryGetProperty("closed_at", out var ca) && ca.ValueKind != JsonValueKind.Null ? ca.GetDateTime() : null,
                MergedAt = prNode.TryGetProperty("merged_at", out var ma) && ma.ValueKind != JsonValueKind.Null ? ma.GetDateTime() : null,
                Additions = prNode.TryGetProperty("additions", out var add) ? add.GetInt32() : 0,
                Deletions = prNode.TryGetProperty("deletions", out var del) ? del.GetInt32() : 0,
                RepositoryId = repo.Id
            }, ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Webhook PR {Action}: #{Number} for {Repo}", action, prNode.GetProperty("number").GetInt32(), repoFullName);
        return Ok(new { message = $"PR event '{action}' processed." });
    }

    private static PullRequestState MapPrState(JsonElement pr)
    {
        if (pr.TryGetProperty("merged_at", out var m) && m.ValueKind != JsonValueKind.Null) return PullRequestState.Merged;
        var state = pr.GetProperty("state").GetString();
        return state == "closed" ? PullRequestState.Closed : PullRequestState.Open;
    }

    private static bool VerifySignature(string payload, string signatureHeader, string secret)
    {
        if (!signatureHeader.StartsWith("sha256=")) return false;
        var expected = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var actual = Convert.ToHexString(hash).ToLower();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
    }
}
