using System.Security.Claims;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
using DevInsight.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevInsight.API.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class SyncController : ControllerBase
{
    private readonly IIntegrationRepository _integrationRepo;
    private readonly IRepository<Repository> _repoRepo;
    private readonly ICommitRepository _commitRepo;
    private readonly IPullRequestRepository _prRepo;
    private readonly IGitProviderFactory _providerFactory;
    private readonly ITokenEncryptionService _encryption;
    private readonly IUnitOfWork _unitOfWork;

    public SyncController(
        IIntegrationRepository integrationRepo, IRepository<Repository> repoRepo,
        ICommitRepository commitRepo, IPullRequestRepository prRepo, IGitProviderFactory providerFactory,
        ITokenEncryptionService encryption, IUnitOfWork unitOfWork)
    {
        _integrationRepo = integrationRepo; _repoRepo = repoRepo;
        _commitRepo = commitRepo; _prRepo = prRepo; _providerFactory = providerFactory;
        _encryption = encryption; _unitOfWork = unitOfWork;
    }

    /// <summary>Sync all repositories for a given provider (github or gitlab).</summary>
    [HttpPost("repos/{provider}")]
    public async Task<IActionResult> SyncRepos(string provider, CancellationToken ct)
    {
        var (orgId, integration, gitProvider) = await ResolveIntegration(provider, ct);
        if (integration is null) return BadRequest(new { message = $"No {provider} integration found. Add one first." });

        var svc = _providerFactory.GetService(gitProvider);
        var token = _encryption.Decrypt(integration.EncryptedAccessToken);
        var remoteRepos = await svc.GetRepositoriesAsync(token, ct);

        var existingRepos = await _repoRepo.GetAllAsync(ct);
        var existingIds = existingRepos.Where(r => r.OrganizationId == orgId).Select(r => r.ExternalId).ToHashSet();

        int added = 0;
        var newRepos = remoteRepos.Where(r => !existingIds.Contains(r.ExternalId)).ToList();
        foreach (var repo in newRepos)
        {
            repo.OrganizationId = orgId;
            await _repoRepo.AddAsync(repo, ct);
            added++;
        }
        await _unitOfWork.SaveChangesAsync(ct);

        // Auto-sync commits + PRs for newly discovered repos
        foreach (var newRepo in newRepos)
        {
            var saved = (await _repoRepo.GetAllAsync(ct)).FirstOrDefault(r => r.ExternalId == newRepo.ExternalId && r.OrganizationId == orgId);
            if (saved is null) continue;
            var commits = await svc.GetCommitsAsync(token, saved.FullName, null, ct);
            foreach (var commit in commits) { commit.RepositoryId = saved.Id; await _commitRepo.AddAsync(commit, ct); }
            var prs = await svc.GetPullRequestsAsync(token, saved.FullName, ct);
            foreach (var pr in prs) { pr.RepositoryId = saved.Id; await _prRepo.AddAsync(pr, ct); }
        }
        if (newRepos.Count > 0)
        {
            integration.LastSyncedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return Ok(new { message = $"Synced {remoteRepos.Count} repos from {provider}. {added} new.", total = remoteRepos.Count, newlyAdded = added });
    }

    /// <summary>Sync commits for a specific repository.</summary>
    [HttpPost("commits/{repositoryId:guid}")]
    public async Task<IActionResult> SyncCommits(Guid repositoryId, CancellationToken ct)
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId, ct);
        if (repo is null) return NotFound(new { message = "Repository not found." });

        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var integration = await _integrationRepo.GetByOrganizationAndProviderAsync(orgId, repo.Provider, ct);
        if (integration is null) return BadRequest(new { message = $"No {repo.Provider} integration found." });

        var svc = _providerFactory.GetService(repo.Provider);
        var token = _encryption.Decrypt(integration.EncryptedAccessToken);
        var existingShas = (await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct)).Select(c => c.Sha).ToHashSet();

        // Only use since-filter if we already have commits — otherwise do a full fetch
        var since = existingShas.Count > 0 ? integration.LastSyncedAt : null;
        var remoteCommits = await svc.GetCommitsAsync(token, repo.FullName, since, ct);

        int added = 0;
        foreach (var commit in remoteCommits.Where(c => !existingShas.Contains(c.Sha)))
        {
            commit.RepositoryId = repositoryId;
            await _commitRepo.AddAsync(commit, ct);
            added++;
        }
        integration.LastSyncedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = $"Synced commits for {repo.FullName}. {added} new.", total = remoteCommits.Count, newlyAdded = added });
    }

    /// <summary>Sync pull requests for a specific repository.</summary>
    [HttpPost("pull-requests/{repositoryId:guid}")]
    public async Task<IActionResult> SyncPullRequests(Guid repositoryId, CancellationToken ct)
    {
        var repo = await _repoRepo.GetByIdAsync(repositoryId, ct);
        if (repo is null) return NotFound(new { message = "Repository not found." });

        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var integration = await _integrationRepo.GetByOrganizationAndProviderAsync(orgId, repo.Provider, ct);
        if (integration is null) return BadRequest(new { message = $"No {repo.Provider} integration found." });

        var svc = _providerFactory.GetService(repo.Provider);
        var token = _encryption.Decrypt(integration.EncryptedAccessToken);
        var remotePrs = await svc.GetPullRequestsAsync(token, repo.FullName, ct);

        var existingIds = (await _prRepo.GetByRepositoryIdAsync(repositoryId, ct)).Select(p => p.ExternalId).ToHashSet();

        int added = 0;
        foreach (var pr in remotePrs.Where(p => !existingIds.Contains(p.ExternalId)))
        {
            pr.RepositoryId = repositoryId;
            await _prRepo.AddAsync(pr, ct);
            added++;
        }
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = $"Synced PRs for {repo.FullName}. {added} new.", total = remotePrs.Count, newlyAdded = added });
    }

    private async Task<(Guid orgId, Integration? integration, GitProvider provider)> ResolveIntegration(string providerName, CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var provider = providerName.ToLower() switch
        {
            "github" => GitProvider.GitHub,
            "gitlab" => GitProvider.GitLab,
            _ => throw new ArgumentException($"Unknown provider: {providerName}")
        };
        var integration = await _integrationRepo.GetByOrganizationAndProviderAsync(orgId, provider, ct);
        return (orgId, integration, provider);
    }

    /// <summary>List all synced repositories for the current org.</summary>
    [HttpGet("repos")]
    public async Task<IActionResult> ListRepos(CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var all = await _repoRepo.GetAllAsync(ct);
        var mine = all.Where(r => r.OrganizationId == orgId).Select(r => new { r.Id, r.Name, r.FullName, Provider = r.Provider.ToString() });
        return Ok(mine);
    }
}
