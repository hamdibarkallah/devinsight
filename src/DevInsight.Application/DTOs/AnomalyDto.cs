namespace DevInsight.Application.DTOs;

public record AnomalyDto(
    string Type,
    string Description,
    string Severity,
    DateTime DetectedAt,
    object Details
);
