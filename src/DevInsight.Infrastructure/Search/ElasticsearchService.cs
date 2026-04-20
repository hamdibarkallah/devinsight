using System.Net.Http.Json;
using System.Text.Json;
using DevInsight.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DevInsight.Infrastructure.Search;

public class ElasticsearchService : ISearchService
{
    private readonly HttpClient _client;
    private readonly ILogger<ElasticsearchService> _logger;
    private bool _available;

    public bool IsAvailable => _available;

    public ElasticsearchService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ElasticsearchService> logger)
    {
        _logger = logger;
        _client = httpFactory.CreateClient("Elasticsearch");
        var url = config["Elasticsearch:Url"];
        if (!string.IsNullOrEmpty(url))
        {
            _client.BaseAddress = new Uri(url);
            _available = true;
        }
    }

    public async Task IndexCommitAsync(Guid commitId, string sha, string message, string authorName, string repoFullName, DateTime authoredAt, CancellationToken ct = default)
    {
        if (!_available) return;
        try
        {
            var doc = new { sha, message, authorName, repoFullName, authoredAt };
            await _client.PutAsJsonAsync($"/devinsight-commits/_doc/{commitId}", doc, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "ES index commit failed"); _available = false; }
    }

    public async Task IndexIssueAsync(Guid issueId, string key, string summary, string? assignee, string status, CancellationToken ct = default)
    {
        if (!_available) return;
        try
        {
            var doc = new { key, summary, assignee, status };
            await _client.PutAsJsonAsync($"/devinsight-issues/_doc/{issueId}", doc, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "ES index issue failed"); _available = false; }
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string? index = null, int maxResults = 20, CancellationToken ct = default)
    {
        if (!_available) return Array.Empty<SearchResult>();
        try
        {
            var target = index ?? "devinsight-commits,devinsight-issues";
            var body = new { size = maxResults, query = new { multi_match = new { query, fields = new[] { "message", "summary", "authorName", "key", "sha" } } } };
            var response = await _client.PostAsJsonAsync($"/{target}/_search", body, ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var hits = json.GetProperty("hits").GetProperty("hits");

            return hits.EnumerateArray().Select(h => new SearchResult(
                h.GetProperty("_index").GetString()!,
                h.GetProperty("_id").GetString()!,
                h.GetProperty("_score").GetDouble(),
                JsonSerializer.Deserialize<Dictionary<string, object>>(h.GetProperty("_source").GetRawText())!
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ES search failed");
            return Array.Empty<SearchResult>();
        }
    }
}
