using DevInsight.Domain.Common;
using DevInsight.Domain.Enums;
namespace DevInsight.Domain.Entities;
public class PullRequest : BaseEntity
{
    public string ExternalId { get; set; } = default!;
    public int Number { get; set; }
    public string Title { get; set; } = default!;
    public PullRequestState State { get; set; }
    public string AuthorName { get; set; } = default!;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? MergedAt { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = default!;
}
