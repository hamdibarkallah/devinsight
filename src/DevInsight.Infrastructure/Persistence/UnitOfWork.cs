using DevInsight.Domain.Interfaces;
namespace DevInsight.Infrastructure.Persistence;
public class UnitOfWork : IUnitOfWork
{
    private readonly DevInsightDbContext _context;
    public UnitOfWork(DevInsightDbContext context) => _context = context;
    public async Task<int> SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
    public void Dispose() => _context.Dispose();
}
