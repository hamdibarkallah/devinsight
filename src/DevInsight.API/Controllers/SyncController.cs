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
        foreach (var repo in remoteRepos.Where(r => !existingIds.Contains(r.ExternalId)))
        {
            repo.OrganizationId = orgId;
            await _repoRepo.AddAsync(repo, ct);
            added++;
        }
        await _unitOfWork.SaveChangesAsync(ct);

        // Auto-sync commits + PRs for every repo owned by this org
        var allRepos = (await _repoRepo.GetAllAsync(ct)).Where(r => r.OrganizationId == orgId).ToList();
        foreach (var repo in allRepos)
        {
            try { await SyncCommitsInternal(repo.Id, svc, token, ct); } catch { }
            try { await SyncPrsInternal(repo.Id, svc, token, ct); } catch { }
        }

        return Ok(new { message = $"Synced {remoteRepos.Count} repos from {provider}. {added} new.", total = remoteRepos.Count, newlyAdded = added });
    }

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
        int added = await SyncCommitsInternal(repositoryId, svc, token, ct);

        return Ok(new { message = $"Synced commits for {repo.FullName}. {added} new.", newlyAdded = added });
    }

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
        int added = await SyncPrsInternal(repositoryId, svc, token, ct);

        return Ok(new { message = $"Synced PRs for {repo.FullName}. {added} new.", newlyAdded = added });
    }

    /// <summary>Always fetches ALL commits from GitHub. Deduplicates by SHA. No timestamps.</summary>
    private async Task<int> SyncCommitsInternal(Guid repositoryId, IGitProviderService svc, string token, CancellationToken ct)
    {
        var repo = (await _repoRepo.GetAllAsync(ct)).FirstOrDefault(r => r.Id == repositoryId);
        if (repo is null) return 0;

        var remoteCommits = await svc.GetCommitsAsync(token, repo.FullName, null, ct);
        var existingShas = (await _commitRepo.GetByRepositoryIdAsync(repositoryId, ct)).Select(c => c.Sha).ToHashSet();

        int added = 0;
        foreach (var commit in remoteCommits.Where(c => !existingShas.Contains(c.Sha)))
        {
            commit.RepositoryId = repositoryId;
            await _commitRepo.AddAsync(commit, ct);
            added++;
        }
        if (added > 0) await _unitOfWork.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>Always fetches ALL PRs. Deduplicates by ExternalId.</summary>
    private async Task<int> SyncPrsInternal(Guid repositoryId, IGitProviderService svc, string token, CancellationToken ct)
    {
        var repo = (await _repoRepo.GetAllAsync(ct)).FirstOrDefault(r => r.Id == repositoryId);
        if (repo is null) return 0;

        var remotePrs = await svc.GetPullRequestsAsync(token, repo.FullName, ct);
        var existingIds = (await _prRepo.GetByRepositoryIdAsync(repositoryId, ct)).Select(p => p.ExternalId).ToHashSet();

        int added = 0;
        foreach (var pr in remotePrs.Where(p => !existingIds.Contains(p.ExternalId)))
        {
            pr.RepositoryId = repositoryId;
            await _prRepo.AddAsync(pr, ct);
            added++;
        }
        if (added > 0) await _unitOfWork.SaveChangesAsync(ct);
        return added;
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

    [HttpGet("repos")]
    public async Task<IActionResult> ListRepos(CancellationToken ct)
    {
        var orgId = Guid.Parse(User.FindFirstValue("org_id")!);
        var all = await _repoRepo.GetAllAsync(ct);
        var mine = all.Where(r => r.OrganizationId == orgId).Select(r => new { r.Id, r.Name, r.FullName, Provider = r.Provider.ToString() });
        return Ok(mine);
    }
}
