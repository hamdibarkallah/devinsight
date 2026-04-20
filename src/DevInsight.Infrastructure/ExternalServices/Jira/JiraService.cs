using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using DevInsight.Application.Common;
using DevInsight.Domain.Entities;
using DevInsight.Domain.Enums;

namespace DevInsight.Infrastructure.ExternalServices.Jira;

public class JiraService : IJiraService
{
    private readonly IHttpClientFactory _http;
    public JiraService(IHttpClientFactory http) => _http = http;

    public async Task<IReadOnlyList<Issue>> GetIssuesAsync(string baseUrl, string email, string apiToken, string projectKey, DateTime? since = null, CancellationToken ct = default)
    {
        var client = _http.CreateClient("Jira");
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
        var authBytes = Encoding.ASCII.GetBytes($"{email}:{apiToken}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        var issues = new List<Issue>();
        int startAt = 0;
        var sinceJql = since.HasValue ? $" AND updated >= \"{since.Value:yyyy-MM-dd}\"" : "";
        var jql = Uri.EscapeDataString($"project = {projectKey}{sinceJql} ORDER BY created DESC");

        while (true)
        {
            var response = await client.GetFromJsonAsync<JiraSearchResponse>(
                $"/rest/api/3/search?jql={jql}&startAt={startAt}&maxResults=100&fields=summary,status,assignee,issuetype,priority,created,resolutiondate", ct);

            if (response is null || response.Issues.Count == 0) break;

            issues.AddRange(response.Issues.Select(ji =>
            {
                var status = MapStatus(ji.Fields.Status?.Name);
                var created = ji.Fields.Created;
                var resolved = ji.Fields.ResolutionDate;
                double? cycleTime = resolved.HasValue ? (resolved.Value - created).TotalHours : null;

                return new Issue
                {
                    ExternalId = ji.Id, Key = ji.Key,
                    Summary = ji.Fields.Summary.Length > 500 ? ji.Fields.Summary[..500] : ji.Fields.Summary,
                    Status = status,
                    Assignee = ji.Fields.Assignee?.DisplayName,
                    IssueType = ji.Fields.IssueType?.Name,
                    Priority = ji.Fields.Priority?.Name,
                    CreatedDate = created, ResolvedDate = resolved,
                    CycleTimeHours = cycleTime.HasValue ? Math.Round(cycleTime.Value, 1) : null
                };
            }));

            startAt += response.Issues.Count;
            if (startAt >= response.Total) break;
        }
        return issues;
    }

    private static IssueStatus MapStatus(string? name) => name?.ToLower() switch
    {
        "to do" or "open" or "backlog" => IssueStatus.ToDo,
        "in progress" or "in review" => IssueStatus.InProgress,
        "done" or "resolved" => IssueStatus.Done,
        "closed" => IssueStatus.Closed,
        _ => IssueStatus.ToDo
    };
}

// --- Jira API response models ---
public class JiraSearchResponse
{
    [JsonPropertyName("startAt")] public int StartAt { get; set; }
    [JsonPropertyName("maxResults")] public int MaxResults { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
    [JsonPropertyName("issues")] public List<JiraIssue> Issues { get; set; } = new();
}
public class JiraIssue
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("fields")] public JiraFields Fields { get; set; } = new();
}
public class JiraFields
{
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("status")] public JiraNameField? Status { get; set; }
    [JsonPropertyName("assignee")] public JiraAssignee? Assignee { get; set; }
    [JsonPropertyName("issuetype")] public JiraNameField? IssueType { get; set; }
    [JsonPropertyName("priority")] public JiraNameField? Priority { get; set; }
    [JsonPropertyName("created")] public DateTime Created { get; set; }
    [JsonPropertyName("resolutiondate")] public DateTime? ResolutionDate { get; set; }
}
public class JiraNameField { [JsonPropertyName("name")] public string Name { get; set; } = ""; }
public class JiraAssignee { [JsonPropertyName("displayName")] public string DisplayName { get; set; } = ""; }
