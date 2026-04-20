using DevInsight.Domain.Entities;
namespace DevInsight.Domain.Interfaces;
public interface IPullRequestRepository : IRepository<PullRequest>
{
    Task<IReadOnlyList<PullRequest>> GetByRepositoryIdAsync(Guid repositoryId, CancellationToken ct = default);
}
