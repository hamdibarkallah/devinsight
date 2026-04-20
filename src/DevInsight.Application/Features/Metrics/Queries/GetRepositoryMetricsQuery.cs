using DevInsight.Application.DTOs;
using MediatR;
namespace DevInsight.Application.Features.Metrics.Queries;
public record GetRepositoryMetricsQuery(Guid RepositoryId, DateTime From, DateTime To) : IRequest<RepositoryMetricsDto>;
