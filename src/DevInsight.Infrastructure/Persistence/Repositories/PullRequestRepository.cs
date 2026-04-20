using DevInsight.Domain.Entities;
using DevInsight.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace DevInsight.Infrastructure.Persistence.Repositories;
public class PullRequestRepository : GenericRepository<PullRequest>, IPullRequestRepository
{
    public PullRequestRepository(DevInsightDbContext context) : base(context) { }
    public async Task<IReadOnlyList<PullRequest>> GetByRepositoryIdAsync(Guid repositoryId, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().Where(pr => pr.RepositoryId == repositoryId).OrderByDescending(pr => pr.OpenedAt).ToListAsync(ct);
}
