using System.Text.Json;
using DevInsight.Application.Common;
using Microsoft.Extensions.Caching.Memory;
namespace DevInsight.Infrastructure.Caching;
public class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    public InMemoryCacheService(IMemoryCache cache) => _cache = cache;
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        _cache.TryGetValue(key, out string? data);
        return Task.FromResult(data is null ? default : JsonSerializer.Deserialize<T>(data));
    }
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        _cache.Set(key, JsonSerializer.Serialize(value), new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5) });
        return Task.CompletedTask;
    }
    public Task RemoveAsync(string key, CancellationToken ct = default) { _cache.Remove(key); return Task.CompletedTask; }
}
