using DevInsight.Domain.Common;
using DevInsight.Domain.Enums;
namespace DevInsight.Domain.Entities;
public class Repository : BaseEntity
{
    public string ExternalId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public GitProvider Provider { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = default!;
    public DateTime? CommitsSyncedAt { get; set; }
    public ICollection<Commit> Commits { get; set; } = new List<Commit>();
    public ICollection<PullRequest> PullRequests { get; set; } = new List<PullRequest>();
}
