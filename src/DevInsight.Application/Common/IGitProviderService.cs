using DevInsight.Domain.Entities;
namespace DevInsight.Application.Common;
public interface IGitProviderService
{
    Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<Commit>> GetCommitsAsync(string accessToken, string repoFullName, DateTime? since = null, CancellationToken ct = default);
    Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(string accessToken, string repoFullName, CancellationToken ct = default);
}
