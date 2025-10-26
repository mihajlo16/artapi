using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace PrviProjekat;

public class Cache
{
    private readonly IMemoryCache _cache;
    private static readonly ConcurrentDictionary<string, object> _keyLocks = new();

    public Cache()
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 512
        });
    }

    public string? GetOrAdd(string key, Func<string> valueFactory)
    {
        if (_cache.TryGetValue(key, out string? existing))
        {
            Logger.Info($"Value returned from cache for key: {key}");
            return existing;
        }

        var keyLock = _keyLocks.GetOrAdd(key, _ => new object());

        lock (keyLock)
        {
            if (_cache.TryGetValue(key, out existing))
                return existing;

            string newValue = valueFactory();

            _cache.Set(
                key,
                newValue,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3),
                    Size = 1
                });

            _keyLocks.TryRemove(key, out _);
            Logger.Info($"Value added to cache for key: {key}");

            return newValue;
        }
    }
}