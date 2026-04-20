using DevInsight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace DevInsight.Infrastructure.Persistence;
public class DevInsightDbContext : DbContext
{
    public DevInsightDbContext(DbContextOptions<DevInsightDbContext> options) : base(options) { }
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Repository> Repositories => Set<Repository>();
    public DbSet<Commit> Commits => Set<Commit>();
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<PullRequest> PullRequests => Set<PullRequest>();
    public DbSet<Issue> Issues => Set<Issue>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Organization>(e => { e.HasKey(o => o.Id); e.Property(o => o.Name).HasMaxLength(200).IsRequired(); });
        modelBuilder.Entity<AppUser>(e => { e.HasKey(u => u.Id); e.Property(u => u.Email).HasMaxLength(256).IsRequired(); e.HasIndex(u => u.Email).IsUnique(); });
        modelBuilder.Entity<Repository>(e => { e.HasKey(r => r.Id); e.Property(r => r.Name).HasMaxLength(300).IsRequired(); e.HasIndex(r => r.ExternalId).IsUnique(); });
        modelBuilder.Entity<Commit>(e => { e.HasKey(c => c.Id); e.Property(c => c.Sha).HasMaxLength(40).IsRequired(); e.HasIndex(c => new { c.RepositoryId, c.Sha }).IsUnique(); });
        modelBuilder.Entity<Integration>(e => { e.HasKey(i => i.Id); });
        modelBuilder.Entity<PullRequest>(e => { e.HasKey(pr => pr.Id); e.HasIndex(pr => new { pr.RepositoryId, pr.ExternalId }).IsUnique(); });
        modelBuilder.Entity<Issue>(e => { e.HasKey(i => i.Id); e.HasIndex(i => i.ExternalId).IsUnique(); });
    }
}
