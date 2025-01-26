using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;

namespace TalkingBot.Services;

// NOTE: This is a service
public class MessageCacher {
    private readonly ILogger<MessageCacher> _logger;
    private readonly Cache<RoleMessageCache> _cache;
    public List<RoleMessageCache> cachedMessages;

    public MessageCacher(ILogger<MessageCacher> logger, Cache<RoleMessageCache> cache) {
        _cache = cache;
        _logger = logger;

        cachedMessages = cache.LoadCached()?.ToList() ?? [];
    }

    public void AddMessage(RoleMessageCache cache) {
        // Save on every message to never lose data
        cachedMessages.Add(cache);
        _cache.SaveCached(cachedMessages.ToArray());
    }
}
