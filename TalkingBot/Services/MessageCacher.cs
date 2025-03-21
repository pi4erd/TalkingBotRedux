using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;

namespace TalkingBot.Services;

public class MessageCacher(ILogger<MessageCacher> logger, Cache<RoleMessageCache[]> cache)
    : IDisposable
{
    private readonly ILogger<MessageCacher> _logger = logger;
    private readonly Cache<RoleMessageCache[]> _cache = cache;
    public List<RoleMessageCache> cachedMessages = cache.LoadCached(CacheName)?.ToList() ?? [];
    private const string CacheName = "MessageCacher";

    public void AddMessage(RoleMessageCache cache) {
        // Save on every message to never lose data
        cachedMessages.Add(cache);
        _cache.SaveCached([.. cachedMessages], CacheName);

        _logger.LogDebug("Added role message and saved cache.");
    }

    public void Dispose()
    {
        _logger.LogInformation("Saving MessageCacher cache on disposal.");
        _cache.SaveCached([.. cachedMessages], CacheName);
        GC.SuppressFinalize(this);
    }
}
