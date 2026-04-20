using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;
namespace DevInsight.Infrastructure.ExternalServices.GitHub;

public class GitHubService : IGitProviderService
{
    private readonly IHttpClientFactory _http;
    public GitHubService(IHttpClientFactory http) => _http = http;

    private HttpClient Client(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || !System.Text.Encoding.ASCII.GetString(System.Text.Encoding.ASCII.GetBytes(token)) .Equals(token, StringComparison.Ordinal))
            throw new InvalidOperationException("GitHub access token is invalid or corrupted. Please re-connect your GitHub integration.");
        var c = _http.CreateClient("GitHub");
        c.BaseAddress = new Uri("https://api.github.com");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
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
            var list = await client.GetFromJsonAsync<List<GhRepo>>($"/user/repos?per_page=100&page={page}&sort=updated", ct);
            if (list is null || list.Count == 0) break;
            repos.AddRange(list.Select(r => new Repository { ExternalId = r.Id.ToString(), Name = r.Name, FullName = r.FullName, Provider = GitProvider.GitHub }));
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
        var sinceParam = since.HasValue ? $"&since={since.Value:O}" : "";
        while (true)
        {
            var list = await client.GetFromJsonAsync<List<GhCommit>>($"/repos/{repoFullName}/commits?per_page=100&page={page}{sinceParam}", ct);
            if (list is null || list.Count == 0) break;
            commits.AddRange(list.Select(c => new Commit
            {
                Sha = c.Sha,
                Message = c.CommitDetail.Message.Length > 500 ? c.CommitDetail.Message[..500] : c.CommitDetail.Message,
                AuthorName = c.CommitDetail.Author.Name, AuthorEmail = c.CommitDetail.Author.Email,
                AuthoredAt = c.CommitDetail.Author.Date, Additions = c.Stats?.Additions ?? 0, Deletions = c.Stats?.Deletions ?? 0
            }));
            if (list.Count < 100) break;
            page++;
        }
        return commits;
    }

    public async Task<IReadOnlyList<PullRequest>> GetPullRequestsAsync(string accessToken, string repoFullName, CancellationToken ct = default)
    {
        var client = Client(accessToken);
        var prs = new List<PullRequest>();
        int page = 1;
        while (true)
        {
            var list = await client.GetFromJsonAsync<List<GhPullRequest>>($"/repos/{repoFullName}/pulls?state=all&per_page=100&page={page}", ct);
            if (list is null || list.Count == 0) break;
            prs.AddRange(list.Select(pr => new PullRequest
            {
                ExternalId = pr.Id.ToString(), Number = pr.Number,
                Title = pr.Title.Length > 300 ? pr.Title[..300] : pr.Title,
                State = pr.MergedAt.HasValue ? PullRequestState.Merged : pr.State == "closed" ? PullRequestState.Closed : PullRequestState.Open,
                AuthorName = pr.User?.Login ?? "unknown",
                OpenedAt = pr.CreatedAt, ClosedAt = pr.ClosedAt, MergedAt = pr.MergedAt,
                Additions = pr.Additions, Deletions = pr.Deletions
            }));
            if (list.Count < 100) break;
            page++;
        }
        return prs;
    }
}

public class GhRepo { [JsonPropertyName("id")] public long Id { get; set; } [JsonPropertyName("name")] public string Name { get; set; } = ""; [JsonPropertyName("full_name")] public string FullName { get; set; } = ""; }
public class GhCommit { [JsonPropertyName("sha")] public string Sha { get; set; } = ""; [JsonPropertyName("commit")] public GhCommitDetail CommitDetail { get; set; } = new(); [JsonPropertyName("stats")] public GhStats? Stats { get; set; } }
public class GhCommitDetail { [JsonPropertyName("message")] public string Message { get; set; } = ""; [JsonPropertyName("author")] public GhAuthor Author { get; set; } = new(); }
public class GhAuthor { [JsonPropertyName("name")] public string Name { get; set; } = ""; [JsonPropertyName("email")] public string Email { get; set; } = ""; [JsonPropertyName("date")] public DateTime Date { get; set; } }
public class GhStats { [JsonPropertyName("additions")] public int Additions { get; set; } [JsonPropertyName("deletions")] public int Deletions { get; set; } }
public class GhPullRequest { [JsonPropertyName("id")] public long Id { get; set; } [JsonPropertyName("number")] public int Number { get; set; } [JsonPropertyName("title")] public string Title { get; set; } = ""; [JsonPropertyName("state")] public string State { get; set; } = ""; [JsonPropertyName("user")] public GhUser? User { get; set; } [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; } [JsonPropertyName("closed_at")] public DateTime? ClosedAt { get; set; } [JsonPropertyName("merged_at")] public DateTime? MergedAt { get; set; } [JsonPropertyName("additions")] public int Additions { get; set; } [JsonPropertyName("deletions")] public int Deletions { get; set; } }
public class GhUser { [JsonPropertyName("login")] public string Login { get; set; } = ""; }
