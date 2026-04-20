using DevInsight.Domain.Entities;
namespace DevInsight.Domain.Interfaces;
public interface IIssueRepository : IRepository<Issue>
{
    Task<IReadOnlyList<Issue>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default);
}
