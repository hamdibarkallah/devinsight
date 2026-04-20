using System.Text.Json;
using DevInsight.Application.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
namespace DevInsight.Infrastructure.Caching;
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger) { _cache = cache; _logger = logger; }
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try { var d = await _cache.GetStringAsync(key, ct); return d is null ? default : JsonSerializer.Deserialize<T>(d); }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache GET failed for {Key}", key); return default; }
    }
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default)
    {
        try { await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5) }, ct); }
        catch (Exception ex) { _logger.LogWarning(ex, "Cache SET failed for {Key}", key); }
    }
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await _cache.RemoveAsync(key, ct); } catch (Exception ex) { _logger.LogWarning(ex, "Cache REMOVE failed for {Key}", key); }
    }
}
