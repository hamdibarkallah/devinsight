using DevInsight.Domain.Entities;
using DevInsight.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace DevInsight.Infrastructure.Persistence.Repositories;
public class CommitRepository : GenericRepository<Commit>, ICommitRepository
{
    public CommitRepository(DevInsightDbContext context) : base(context) { }
    public async Task<IReadOnlyList<Commit>> GetByRepositoryIdAsync(Guid repositoryId, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().Where(c => c.RepositoryId == repositoryId).OrderByDescending(c => c.AuthoredAt).ToListAsync(ct);
}
