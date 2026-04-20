namespace DevInsight.Application.DTOs;
public record RepositoryMetricsDto(
    int TotalCommits,
    int TotalAdditions,
    int TotalDeletions,
    int UniqueAuthors,
    DateTime? FirstCommit,
    DateTime? LastCommit
);
