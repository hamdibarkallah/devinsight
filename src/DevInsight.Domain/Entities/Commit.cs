using DevInsight.Domain.Common;
namespace DevInsight.Domain.Entities;
public class Commit : BaseEntity
{
    public string Sha { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string AuthorName { get; set; } = default!;
    public string AuthorEmail { get; set; } = default!;
    public DateTime AuthoredAt { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public Guid RepositoryId { get; set; }
    public Repository Repository { get; set; } = default!;
}
