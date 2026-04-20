using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
namespace DevInsight.Domain.Interfaces;
public interface IIntegrationRepository : IRepository<Integration>
{
    Task<Integration?> GetByOrganizationAndProviderAsync(Guid organizationId, GitProvider provider, CancellationToken ct = default);
}
