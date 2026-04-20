using DevInsight.Domain.Common;
using DevInsight.Domain.Enums;
namespace DevInsight.Domain.Entities;
public class Integration : BaseEntity
{
    public GitProvider Provider { get; set; }
    public string EncryptedAccessToken { get; set; } = default!;
    public DateTime? LastSyncedAt { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = default!;
}
