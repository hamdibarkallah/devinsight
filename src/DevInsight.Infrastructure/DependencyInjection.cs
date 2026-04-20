using DevInsight.Application.Common;
using DevInsight.Domain.Interfaces;
using DevInsight.Infrastructure.Persistence;
using DevInsight.Infrastructure.Persistence.Repositories;
using DevInsight.Infrastructure.Security;
using DevInsight.Infrastructure.Caching;
using DevInsight.Infrastructure.BackgroundJobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hangfire;
using Hangfire.InMemory;
using MediatR;
namespace DevInsight.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=devinsight.db";
        services.AddDbContext<DevInsightDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ICommitRepository, CommitRepository>();
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<IPullRequestRepository, PullRequestRepository>();
        services.AddScoped<IIssueRepository, IssueRepository>();
        services.AddScoped<IJiraService, ExternalServices.Jira.JiraService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<ITokenEncryptionService, AesTokenEncryptionService>();
        services.AddHttpClient("GitHub");
        services.AddHttpClient("GitLab");
        services.AddHttpClient("Jira");
        services.AddHttpClient("Elasticsearch");
        services.AddScoped<IGitProviderService, ExternalServices.GitHub.GitHubService>();
        services.AddSingleton<IGitProviderFactory, ExternalServices.GitProviderFactory>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddScoped<SyncAllJob>();
        services.AddHangfire(config => config.UseInMemoryStorage());
        services.AddHangfireServer();
        var redisConn = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConn))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConn);
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        }
        var esUrl = configuration["Elasticsearch:Url"];
        if (!string.IsNullOrEmpty(esUrl))
            services.AddSingleton<ISearchService, Search.ElasticsearchService>();
        else
            services.AddSingleton<ISearchService, Search.NoOpSearchService>();
        return services;
    }
}
