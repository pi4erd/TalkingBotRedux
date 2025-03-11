using System.Linq;
using Microsoft.Extensions.Logging;
using TalkingBot.Core.Caching;

public class GameDataCacher {
    private Dictionary<ulong, UserGameData> cachedData;
    private readonly ILogger<GameDataCacher> _logger;
    private readonly Cache<Dictionary<ulong, UserGameData>> _cache;

    private const string CacheName = "GameDataCacher";

    public GameDataCacher(ILogger<GameDataCacher> logger, Cache<Dictionary<ulong, UserGameData>> cache) {
        _logger = logger;
        _cache = cache;

        cachedData = cache.LoadCached(CacheName) ?? [];
    }

    public UserGameData GetUserGameData(ulong uid) {
        if(!cachedData.ContainsKey(uid)) {
            cachedData.Add(uid, UserGameData.Default());
            _cache.SaveCached(cachedData, CacheName);
        }
        return cachedData[uid];
    }

    public void ModifyUserData(ulong uid, UserGameData data) {
        if(!cachedData.ContainsKey(uid)) {
            cachedData.Add(uid, UserGameData.Default());
        }
        cachedData[uid] = data;
        _cache.SaveCached(cachedData, CacheName);
    }
}
