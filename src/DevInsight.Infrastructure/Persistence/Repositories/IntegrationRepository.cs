using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
using DevInsight.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace DevInsight.Infrastructure.Persistence.Repositories;
public class IntegrationRepository : GenericRepository<Integration>, IIntegrationRepository
{
    public IntegrationRepository(DevInsightDbContext context) : base(context) { }
    public async Task<Integration?> GetByOrganizationAndProviderAsync(Guid organizationId, GitProvider provider, CancellationToken ct = default)
        => await _dbSet.FirstOrDefaultAsync(i => i.OrganizationId == organizationId && i.Provider == provider, ct);
}
