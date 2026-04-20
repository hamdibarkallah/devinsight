using DevInsight.Domain.Common;
using DevInsight.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace DevInsight.Infrastructure.Persistence.Repositories;
public class GenericRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly DevInsightDbContext _context;
    protected readonly DbSet<T> _dbSet;
    public GenericRepository(DevInsightDbContext context) { _context = context; _dbSet = context.Set<T>(); }
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) => await _dbSet.FindAsync(new object[] { id }, ct);
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) => await _dbSet.AsNoTracking().ToListAsync(ct);
    public async Task<T> AddAsync(T entity, CancellationToken ct = default) { await _dbSet.AddAsync(entity, ct); return entity; }
    public void Update(T entity) { _dbSet.Update(entity); }
}
