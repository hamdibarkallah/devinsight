using DevInsight.Domain.Common;
using DevInsight.Domain.Enums;
namespace DevInsight.Domain.Entities;
public class Issue : BaseEntity
{
    public string ExternalId { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public IssueStatus Status { get; set; }
    public string? Assignee { get; set; }
    public string? IssueType { get; set; }
    public string? Priority { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public double? CycleTimeHours { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = default!;
}
