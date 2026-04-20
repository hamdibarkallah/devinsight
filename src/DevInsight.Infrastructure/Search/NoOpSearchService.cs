using DevInsight.Application.Common;

namespace DevInsight.Infrastructure.Search;

public class NoOpSearchService : ISearchService
{
    public bool IsAvailable => false;
    public Task IndexCommitAsync(Guid commitId, string sha, string message, string authorName, string repoFullName, DateTime authoredAt, CancellationToken ct = default) => Task.CompletedTask;
    public Task IndexIssueAsync(Guid issueId, string key, string summary, string? assignee, string status, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string? index = null, int maxResults = 20, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SearchResult>>(Array.Empty<SearchResult>());
}
