namespace DevInsight.Application.DTOs;

public record DeveloperStatsDto(
    string AuthorName,
    string AuthorEmail,
    int TotalCommits,
    int TotalAdditions,
    int TotalDeletions,
    int NetLinesChanged,
    int PullRequestsOpened,
    int PullRequestsMerged,
    double AvgPrLeadTimeHours
);

public record TeamVelocityDto(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int TotalCommits,
    int TotalPrsMerged,
    int TotalAdditions,
    int TotalDeletions,
    int ActiveDevelopers,
    double AvgCommitsPerDeveloper
);

public record TrendDataPointDto(
    DateTime Date,
    int Commits,
    int PrsMerged,
    int Additions,
    int Deletions
);

public record PrCycleTimeDto(
    int PrNumber,
    string Title,
    string Author,
    double LeadTimeHours,
    double? TimeToMergeHours,
    string State
);

public record BottleneckDto(
    string Type,
    string Description,
    string Severity,
    object Details
);
