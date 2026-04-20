using DevInsight.Domain.Entities;
namespace DevInsight.Domain.Interfaces;
public interface ICommitRepository : IRepository<Commit>
{
    Task<IReadOnlyList<Commit>> GetByRepositoryIdAsync(Guid repositoryId, CancellationToken ct = default);
}
