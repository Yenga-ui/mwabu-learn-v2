using Microsoft.Extensions.Caching.Memory;
using MwabuLearn.Application.Lessons;
namespace MwabuLearn.Infrastructure.Lessons.Providers;
public sealed class SearchResultCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 500 });
    public bool Get(string key, out IReadOnlyList<DiscoveredSource>? value) => cache.TryGetValue(key, out value);
    public void Set(string key, IReadOnlyList<DiscoveredSource> value, int minutes) => cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes), Size = 1 });
    public void Dispose() => cache.Dispose();
}
