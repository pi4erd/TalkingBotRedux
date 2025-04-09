using System.Linq;
using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;

namespace TalkingBot.Services;

public class GameDataCacher(
    ILogger<GameDataCacher> logger,
    Cache<Dictionary<ulong, UserGameData>> cache
) : IDisposable {
    private readonly Dictionary<ulong, UserGameData> cachedData = cache.LoadCached(CacheName) ?? [];
    private readonly ILogger<GameDataCacher> _logger = logger;
    private readonly Cache<Dictionary<ulong, UserGameData>> _cache = cache;

    private const string CacheName = "GameDataCacher";

    public UserGameData GetUserGameData(ulong uid) {
        if(!cachedData.TryGetValue(uid, out UserGameData? value)) {
            value = UserGameData.Default();
            cachedData.Add(uid, value);
            _cache.SaveCached(cachedData, CacheName);
        }
        return value;
    }

    public void ModifyUserData(ulong uid, UserGameData data) {
        if(!cachedData.ContainsKey(uid)) {
            cachedData.Add(uid, UserGameData.Default());
        }
        cachedData[uid] = data;
        _cache.SaveCached(cachedData, CacheName);
    }

    public void Dispose()
    {
        _logger.LogInformation("Saving GameDataCacher cache on disposal.");
        _cache.SaveCached(cachedData, CacheName);
        GC.SuppressFinalize(this);
    }
}
