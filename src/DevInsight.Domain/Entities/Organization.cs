using DevInsight.Domain.Common;
namespace DevInsight.Domain.Entities;
public class Organization : BaseEntity
{
    public string Name { get; set; } = default!;
    public ICollection<AppUser> Members { get; set; } = new List<AppUser>();
    public ICollection<Repository> Repositories { get; set; } = new List<Repository>();
}
