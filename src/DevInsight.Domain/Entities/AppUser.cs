using DevInsight.Domain.Common;
namespace DevInsight.Domain.Entities;
public class AppUser : BaseEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = default!;
}
