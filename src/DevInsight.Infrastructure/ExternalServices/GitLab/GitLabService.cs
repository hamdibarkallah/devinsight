using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
namespace DevInsight.Infrastructure.ExternalServices.GitLab;

public class GitLabService : IGitProviderService
{
    private readonly IHttpClientFactory _http;
    public GitLabService(IHttpClientFactory http) => _http = http;

    private HttpClient Client(string token)
    {
        var c = _http.CreateClient("GitLab");
        c.BaseAddress = new Uri("https://gitlab.com/api/v4/");
        c.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DevInsight/1.0");
        return c;
    }

    public async Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string accessToken, CancellationToken ct = default)
    {
        var client = Client(accessToken);
        var repos = new List<Repository>();
        int page = 1;
        while (true)
        {
            var list = await client.GetFromJsonAsync<List<GlProject>>($"projects?membership=true&per_page=100&page={page}", ct);
            if (list is null || list.Count == 0) break;
            repos.AddRange(list.Select(p => new Repository { ExternalId = p.Id.ToString(), Name = p.Name, FullName = p.PathWithNamespace, Provider = GitProvider.GitLab }));
            if (list.Count < 100) break;
            page++;
        }
        return repos;
    }

    public async Task<IReadOnlyList<Commit>> GetCommitsAsync(string accessToken, string repoFullName, DateTime? since = null, CancellationToken ct = default)
    {
        var client = Client(accessToken);
        var commits = new List<Commit>();
        int page = 1;
        var encoded = Uri.EscapeDataString(repoFullName);
        var sinceParam = since.HasValue ? $"&since={since.Value:O}" : "";
        while (true)
        {
            var list = await client.GetFromJsonAsync<List<GlCommit>>($"projects/{encoded}/repository/commits?per_page=100&page={page}{sinceParam}", ct);
            if (list is null || list.Count == 0) break;
            commits.AddRange(list.Select(c => new Commit { Sha = c.Id, Message = c.Message.Length > 500 ? c.Message[..500] : c.Message, AuthorName = c.AuthorName, AuthorEmail = c.AuthorEmail, AuthoredAt = c.AuthoredDate }));
            if (list.Count < 100) break;
            page++;
        }
        return commits;
    }

    public async Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(string accessToken, string repoFullName, CancellationToken ct = default)
    {
        var client = Client(accessToken);
        var mrs = new List<PullRequest>();
        int page = 1;
        var encoded = Uri.EscapeDataString(repoFullName);
        while (true)
        {
            var list = await client.GetFromJsonAsync<List<GlMergeRequest>>($"projects/{encoded}/merge_requests?state=all&per_page=100&page={page}", ct);
            if (list is null || list.Count == 0) break;
            mrs.AddRange(list.Select(mr => new PullRequest
            {
                ExternalId = mr.Id.ToString(), Number = mr.Iid, Title = mr.Title.Length > 300 ? mr.Title[..300] : mr.Title,
                State = mr.MergedAt.HasValue ? PullRequestState.Merged : mr.State == "closed" ? PullRequestState.Closed : PullRequestState.Open,
                AuthorName = mr.Author?.Username ?? "unknown", OpenedAt = mr.CreatedAt, ClosedAt = mr.ClosedAt, MergedAt = mr.MergedAt
            }));
            if (list.Count < 100) break;
            page++;
        }
        return mrs;
    }
}

public class GlProject { [JsonPropertyName("id")] public long Id { get; set; } [JsonPropertyName("name")] public string Name { get; set; } = ""; [JsonPropertyName("path_with_namespace")] public string PathWithNamespace { get; set; } = ""; }
public class GlCommit { [JsonPropertyName("id")] public string Id { get; set; } = ""; [JsonPropertyName("message")] public string Message { get; set; } = ""; [JsonPropertyName("author_name")] public string AuthorName { get; set; } = ""; [JsonPropertyName("author_email")] public string AuthorEmail { get; set; } = ""; [JsonPropertyName("authored_date")] public DateTime AuthoredDate { get; set; } }
public class GlMergeRequest { [JsonPropertyName("id")] public long Id { get; set; } [JsonPropertyName("iid")] public int Iid { get; set; } [JsonPropertyName("title")] public string Title { get; set; } = ""; [JsonPropertyName("state")] public string State { get; set; } = ""; [JsonPropertyName("author")] public GlUser? Author { get; set; } [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; } [JsonPropertyName("closed_at")] public DateTime? ClosedAt { get; set; } [JsonPropertyName("merged_at")] public DateTime? MergedAt { get; set; } }
public class GlUser { [JsonPropertyName("username")] public string Username { get; set; } = ""; }
