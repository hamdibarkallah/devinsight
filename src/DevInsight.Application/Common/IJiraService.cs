using DevInsight.Domain.Entities;
namespace DevInsight.Application.Common;
public interface IJiraService
{
    Task<IReadOnlyList<Issue>> GetIssuesAsync(string baseUrl, string email, string apiToken, string projectKey, DateTime? since = null, CancellationToken ct = default);
}
