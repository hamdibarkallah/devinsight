using DevInsight.Application.DTOs;
using DevInsight.Application.Features.Metrics.Queries;
using DevInsight.Domain.Interfaces;
using MediatR;

namespace DevInsight.Infrastructure.Features.Metrics;

public class GetRepositoryMetricsHandler : IRequestHandler<GetRepositoryMetricsQuery, RepositoryMetricsDto>
{
    private readonly ICommitRepository _commitRepo;
    public GetRepositoryMetricsHandler(ICommitRepository commitRepo) => _commitRepo = commitRepo;

    public async Task<RepositoryMetricsDto> Handle(GetRepositoryMetricsQuery request, CancellationToken ct)
    {
        var commits = (await _commitRepo.GetByRepositoryIdAsync(request.RepositoryId, ct))
            .Where(c => c.AuthoredAt >= request.From && c.AuthoredAt <= request.To).ToList();

        return new RepositoryMetricsDto(
            commits.Count,
            commits.Sum(c => c.Additions),
            commits.Sum(c => c.Deletions),
            commits.Select(c => c.AuthorEmail).Distinct().Count(),
            commits.Any() ? commits.Min(c => c.AuthoredAt) : null,
            commits.Any() ? commits.Max(c => c.AuthoredAt) : null
        );
    }
}
