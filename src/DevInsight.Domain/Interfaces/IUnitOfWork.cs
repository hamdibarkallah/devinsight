namespace DevInsight.Domain.Interfaces;
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
