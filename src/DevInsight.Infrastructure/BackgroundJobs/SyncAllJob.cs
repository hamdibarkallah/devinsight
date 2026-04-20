using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
using DevInsight.Domain.Interfaces;
using Microsoft.Extensions.Logging;
namespace DevInsight.Infrastructure.BackgroundJobs;
public class SyncAllJob
{
    private readonly IIntegrationRepository _integrationRepo;
    private readonly IRepository<Repository> _repoRepo;
    private readonly ICommitRepository _commitRepo;
    private readonly IGitProviderFactory _providerFactory;
    private readonly ITokenEncryptionService _encryption;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SyncAllJob> _logger;

    public SyncAllJob(IIntegrationRepository integrationRepo, IRepository<Repository> repoRepo,
        ICommitRepository commitRepo, IGitProviderFactory providerFactory,
        ITokenEncryptionService encryption, IUnitOfWork unitOfWork, ILogger<SyncAllJob> logger)
    {
        _integrationRepo = integrationRepo; _repoRepo = repoRepo;
        _commitRepo = commitRepo; _providerFactory = providerFactory;
        _encryption = encryption; _unitOfWork = unitOfWork; _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("SyncAllJob started");
        var integrations = await _integrationRepo.GetAllAsync();
        foreach (var integration in integrations)
        {
            try
            {
                var svc = _providerFactory.GetService(integration.Provider);
                var token = _encryption.Decrypt(integration.EncryptedAccessToken);
                var orgId = integration.OrganizationId;

                var ghRepos = await svc.GetRepositoriesAsync(token);
                var existingRepos = (await _repoRepo.GetAllAsync()).Where(r => r.OrganizationId == orgId).ToList();
                var existingIds = existingRepos.Select(r => r.ExternalId).ToHashSet();
                foreach (var repo in ghRepos.Where(r => !existingIds.Contains(r.ExternalId)))
                {
                    repo.OrganizationId = orgId;
                    await _repoRepo.AddAsync(repo);
                }
                await _unitOfWork.SaveChangesAsync();

                existingRepos = (await _repoRepo.GetAllAsync()).Where(r => r.OrganizationId == orgId).ToList();
                foreach (var repo in existingRepos)
                {
                    try
                    {
                        var commits = await svc.GetCommitsAsync(token, repo.FullName, integration.LastSyncedAt);
                        var existingShas = (await _commitRepo.GetByRepositoryIdAsync(repo.Id)).Select(c => c.Sha).ToHashSet();
                        int added = 0;
                        foreach (var c in commits.Where(c => !existingShas.Contains(c.Sha)))
                        {
                            c.RepositoryId = repo.Id;
                            await _commitRepo.AddAsync(c);
                            added++;
                        }
                        await _unitOfWork.SaveChangesAsync();
                        _logger.LogInformation("Synced {Count} new commits for {Repo}", added, repo.FullName);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to sync commits for {Repo}", repo.FullName); }
                }
                integration.LastSyncedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogError(ex, "Failed to sync for integration {Id}", integration.Id); }
        }
        _logger.LogInformation("SyncAllJob completed");
    }
}
