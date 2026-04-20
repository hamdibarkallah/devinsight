using DevInsight.Application.Common;
using DevInsight.Domain.Enums;
using DevInsight.Infrastructure.ExternalServices.GitHub;
using DevInsight.Infrastructure.ExternalServices.GitLab;
namespace DevInsight.Infrastructure.ExternalServices;
public class GitProviderFactory : IGitProviderFactory
{
    private readonly IHttpClientFactory _http;
    public GitProviderFactory(IHttpClientFactory http) => _http = http;
    public IGitProviderService GetService(GitProvider provider) => provider switch
    {
        GitProvider.GitHub => new GitHubService(_http),
        GitProvider.GitLab => new GitLabService(_http),
        _ => throw new NotSupportedException($"Provider {provider} not supported.")
    };
}
