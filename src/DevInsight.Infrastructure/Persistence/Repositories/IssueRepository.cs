using DevInsight.Domain.Entities;
using DevInsight.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace DevInsight.Infrastructure.Persistence.Repositories;
public class IssueRepository : GenericRepository<Issue>, IIssueRepository
{
    public IssueRepository(DevInsightDbContext context) : base(context) { }
    public async Task<IReadOnlyList<Issue>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
        => await _dbSet.AsNoTracking().Where(i => i.OrganizationId == organizationId).OrderByDescending(i => i.CreatedDate).ToListAsync(ct);
}
