namespace DevInsight.Application.Common;
public interface ISearchService
{
    Task IndexCommitAsync(Guid commitId, string sha, string message, string authorName, string repoFullName, DateTime authoredAt, CancellationToken ct = default);
    Task IndexIssueAsync(Guid issueId, string key, string summary, string? assignee, string status, CancellationToken ct = default);
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string? index = null, int maxResults = 20, CancellationToken ct = default);
    bool IsAvailable { get; }
}
public record SearchResult(string Index, string Id, double Score, Dictionary<string, object> Source);
